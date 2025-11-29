using System.Net;
using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DublinBikes.Api.Services
{
    /// <summary>
    /// Binds CosmosDb configuration from appsettings.json ("CosmosDb" section).
    /// </summary>
    public class CosmosOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Station service implementation backed by Azure Cosmos DB.
    /// This is used by API V2.
    /// </summary>
    public class CosmosStationService : IStationService
    {
        private readonly Container _container;
        private readonly IMemoryCache _cache;
        private const string CachePrefix = "cosmos_stations_query_";

        public CosmosStationService(
            IOptions<CosmosOptions> options,
            IMemoryCache cache)
        {
            _cache = cache;

            var opts = options.Value;

            var client = new CosmosClient(opts.ConnectionString);
            var db = client.CreateDatabaseIfNotExistsAsync(opts.DatabaseName).GetAwaiter().GetResult();
            var container = db.Database.CreateContainerIfNotExistsAsync(
                id: opts.ContainerName,
                partitionKeyPath: "/number"    // partition key = station number
            ).GetAwaiter().GetResult();

            _container = container.Container;
        }

        /// <summary>
        /// Returns paged / filtered / sorted stations from CosmosDb.
        /// For simplicity we load all items then filter in memory.
        /// Results are cached for 5 minutes.
        /// </summary>
        public async Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters)
        {
            // ---------- CACHE ----------
            var cacheKey = CachePrefix +
                           $"{parameters.Status}_{parameters.MinBikes}_{parameters.SearchTerm}_{parameters.Sort}_{parameters.Dir}_{parameters.Page}_{parameters.PageSize}";

            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Station>? cached))
            {
                return cached!;
            }

            // ---------- LOAD FROM COSMOS ----------
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<Station>(query);

            var allStations = new List<Station>();

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                allStations.AddRange(page);
            }

            IEnumerable<Station> filtered = allStations;

            // Filter: status=OPEN|CLOSED
            if (!string.IsNullOrWhiteSpace(parameters.Status))
            {
                var status = parameters.Status.Trim();
                filtered = filtered.Where(s =>
                    s.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Filter: minBikes
            if (parameters.MinBikes.HasValue)
            {
                filtered = filtered.Where(s => s.AvailableBikes >= parameters.MinBikes.Value);
            }

            // Search: q over name/address
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim();
                filtered = filtered.Where(s =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Sorting
            filtered = ApplySorting(filtered, parameters.Sort, parameters.Dir);

            // Paging
            var pageNumber = parameters.Page <= 0 ? 1 : parameters.Page;
            var pageSize = parameters.PageSize <= 0 ? 20 : parameters.PageSize;

            filtered = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            var result = filtered.ToList().AsReadOnly();

            // ---------- CACHE FOR 5 MINUTES ----------
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        private static IEnumerable<Station> ApplySorting(IEnumerable<Station> query, string? sort, string? dir)
        {
            bool desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            switch (sort?.ToLowerInvariant())
            {
                case "availablebikes":
                    query = desc
                        ? query.OrderByDescending(s => s.AvailableBikes)
                        : query.OrderBy(s => s.AvailableBikes);
                    break;

                case "occupancy":
                    query = desc
                        ? query.OrderByDescending(s => s.Occupancy)
                        : query.OrderBy(s => s.Occupancy);
                    break;

                case "name":
                default:
                    query = desc
                        ? query.OrderByDescending(s => s.Name)
                        : query.OrderBy(s => s.Name);
                    break;
            }

            return query;
        }

        public async Task<Station?> GetStationByNumberAsync(int number)
        {
            // We use a query because id is string and partition key is number
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.number = @number")
                .WithParameter("@number", number);

            var iterator = _container.GetItemQueryIterator<Station>(query);
            if (!iterator.HasMoreResults)
                return null;

            var page = await iterator.ReadNextAsync();
            return page.FirstOrDefault();
        }

        public async Task AddStationAsync(Station newStation)
        {
            newStation.Id = newStation.Number.ToString();

            await _container.CreateItemAsync(newStation, new PartitionKey(newStation.Number));

            InvalidateCache();
        }

        public async Task<bool> UpdateStationAsync(int number, Station updatedStation)
        {
            updatedStation.Id = number.ToString();

            try
            {
                await _container.UpsertItemAsync(updatedStation, new PartitionKey(number));
                InvalidateCache();
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<StationsSummaryDto> GetSummaryAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<Station>(query);

            var allStations = new List<Station>();
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                allStations.AddRange(page);
            }

            var summary = new StationsSummaryDto
            {
                TotalStations = allStations.Count,
                TotalBikeStands = allStations.Sum(s => s.BikeStands),
                TotalAvailableBikes = allStations.Sum(s => s.AvailableBikes),
                OpenStations = allStations.Count(s => s.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)),
                ClosedStations = allStations.Count(s => s.Status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
            };

            return summary;
        }

        /// <summary>
        /// Seeds CosmosDb with the stations coming from the file-based service.
        /// </summary>
        public async Task SeedAsync(IEnumerable<Station> stations)
        {
            foreach (var station in stations)
            {
                station.Id = station.Number.ToString();
                await _container.UpsertItemAsync(station, new PartitionKey(station.Number));
            }

            InvalidateCache();
        }

        /// <summary>
        /// Deletes a station from CosmosDb by station number.
        /// </summary>
        public async Task<bool> DeleteAsync(int number)
        {
            try
            {
                await _container.DeleteItemAsync<Station>(
                    id: number.ToString(),
                    partitionKey: new PartitionKey(number));

                InvalidateCache();
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        private void InvalidateCache()
        {
            (_cache as MemoryCache)?.Compact(1.0);
        }
    }
}
