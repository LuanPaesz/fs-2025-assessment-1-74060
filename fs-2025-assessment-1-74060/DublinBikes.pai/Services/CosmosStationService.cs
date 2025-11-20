using System.Net;
using System.Text.Json;
using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;

namespace DublinBikes.Api.Services
{
    // Classe que mapeia a secção CosmosDb do appsettings.json
    public class CosmosOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implementação de IStationService usando Azure Cosmos DB.
    /// V2 da API vai usar este serviço.
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
                partitionKeyPath: "/number"    // usamos o número da estação como partition key
            ).GetAwaiter().GetResult();

            _container = container.Container;
        }

        public async Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters)
        {
            // ---------- CACHE: tenta pegar do cache primeiro ----------
            var cacheKey = CachePrefix +
                           $"{parameters.Status}_{parameters.MinBikes}_{parameters.SearchTerm}_{parameters.Sort}_{parameters.Dir}_{parameters.Page}_{parameters.PageSize}";

            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Station>? cached))
            {
                // Se já está em cache (resultado de outra chamada idêntica),
                // voltamos direto, sem ir ao Cosmos.
                return cached!;
            }

            // ---------- BUSCA NO COSMOS ----------
            // Para simplificar, buscamos tudo e filtramos em memória.
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<Station>(query);

            var allStations = new List<Station>();

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                allStations.AddRange(page);
            }

            IEnumerable<Station> filtered = allStations;

            // Filtro: status=OPEN|CLOSED
            if (!string.IsNullOrWhiteSpace(parameters.Status))
            {
                var status = parameters.Status.Trim();
                filtered = filtered.Where(s =>
                    s.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Filtro: minBikes
            if (parameters.MinBikes.HasValue)
            {
                filtered = filtered.Where(s => s.AvailableBikes >= parameters.MinBikes.Value);
            }

            // Busca: q = SearchTerm em name/address
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim();
                filtered = filtered.Where(s =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Sorting: name | availableBikes | occupancy
            filtered = ApplySorting(filtered, parameters.Sort, parameters.Dir);

            // Paging
            var pageNumber = parameters.Page <= 0 ? 1 : parameters.Page;
            var pageSize = parameters.PageSize <= 0 ? 20 : parameters.PageSize;

            filtered = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            var result = filtered.ToList().AsReadOnly();

            // ---------- GRAVA EM CACHE POR 5 MINUTOS ----------
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        private static IEnumerable<Station> ApplySorting(IEnumerable<Station> query, string sort, string dir)
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
            // usamos uma query porque o id é string e o partition key é number
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
            // garantir id string baseado no número
            newStation.Id = newStation.Number.ToString();

            await _container.CreateItemAsync(newStation, new PartitionKey(newStation.Number));
        }

        public async Task<bool> UpdateStationAsync(int number, Station updatedStation)
        {
            updatedStation.Id = number.ToString();

            try
            {
                await _container.UpsertItemAsync(updatedStation, new PartitionKey(number));
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

        // Método opcional para "semear" o Cosmos com dados do FileStationService
        public async Task SeedAsync(IEnumerable<Station> stations)
        {
            foreach (var station in stations)
            {
                station.Id = station.Number.ToString();
                await _container.UpsertItemAsync(station, new PartitionKey(station.Number));
            }
        }
    }
}
