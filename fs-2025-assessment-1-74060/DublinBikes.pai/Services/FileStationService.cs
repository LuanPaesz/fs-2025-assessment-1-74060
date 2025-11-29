using System.Text.Json;
using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace DublinBikes.Api.Services
{
    /// <summary>
    /// Station service implementation backed by the local JSON file (V1).
    /// Data is loaded once at startup and kept in memory.
    /// All queries are cached in IMemoryCache for 5 minutes.
    /// </summary>
    public class FileStationService : IStationService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<FileStationService> _logger;
        private readonly List<Station> _stations = new();

        private const string CachePrefix = "file_stations_query_";

        public FileStationService(
            IMemoryCache cache,
            IHostEnvironment env,
            ILogger<FileStationService> logger)
        {
            _cache = cache;
            _logger = logger;

            var dataPath = Path.Combine(env.ContentRootPath, "Data", "dublinbike.json");

            if (!File.Exists(dataPath))
            {
                _logger.LogError("DublinBikes data file not found at {Path}", dataPath);
                return;
            }

            try
            {
                var json = File.ReadAllText(dataPath);

                // The JSON is an array of stations
                var stations = JsonSerializer.Deserialize<List<Station>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (stations != null)
                {
                    _stations = stations;
                    _logger.LogInformation("Loaded {Count} stations from {Path}", _stations.Count, dataPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load DublinBikes data from {Path}", dataPath);
            }
        }

        /// <summary>
        /// Returns a paged / filtered / sorted list of stations from the in-memory collection.
        /// Results are cached for 5 minutes based on the query parameters.
        /// </summary>
        public Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters)
        {
            // Build cache key from query parameters
            var cacheKey = CachePrefix +
                           $"{parameters.Status}_{parameters.MinBikes}_{parameters.SearchTerm}_{parameters.Sort}_{parameters.Dir}_{parameters.Page}_{parameters.PageSize}";

            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Station>? cached))
            {
                return Task.FromResult(cached!);
            }

            IEnumerable<Station> query = _stations;

            // Filter: status=OPEN|CLOSED
            if (!string.IsNullOrWhiteSpace(parameters.Status))
            {
                var status = parameters.Status.Trim();
                query = query.Where(s =>
                    s.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Filter: minBikes
            if (parameters.MinBikes.HasValue)
            {
                query = query.Where(s => s.AvailableBikes >= parameters.MinBikes.Value);
            }

            // Search: q over name + address
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim();
                query = query.Where(s =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Sorting
            query = ApplySorting(query, parameters.Sort, parameters.Dir);

            // Paging (defensive defaults)
            var pageNumber = parameters.Page <= 0 ? 1 : parameters.Page;
            var pageSize = parameters.PageSize <= 0 ? 20 : parameters.PageSize;

            query = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            var result = query.ToList().AsReadOnly();

            // Cache for 5 minutes
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return Task.FromResult((IReadOnlyList<Station>)result);
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

        /// <summary>
        /// Returns a single station by its number.
        /// </summary>
        public Task<Station?> GetStationByNumberAsync(int number)
        {
            var station = _stations.FirstOrDefault(s => s.Number == number);
            return Task.FromResult(station);
        }

        /// <summary>
        /// Adds a new station to the in-memory list.
        /// Also clears the cache (so that new data is visible).
        /// </summary>
        public Task AddStationAsync(Station newStation)
        {
            // Basic defensive check: avoid duplicates
            if (_stations.Any(s => s.Number == newStation.Number))
            {
                throw new InvalidOperationException($"Station {newStation.Number} already exists.");
            }

            _stations.Add(newStation);
            InvalidateCache();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates an existing station. Returns false if the station does not exist.
        /// </summary>
        public Task<bool> UpdateStationAsync(int number, Station updatedStation)
        {
            var index = _stations.FindIndex(s => s.Number == number);
            if (index == -1)
            {
                return Task.FromResult(false);
            }

            _stations[index] = updatedStation;
            InvalidateCache();

            return Task.FromResult(true);
        }

        /// <summary>
        /// Deletes a station by number. Returns false if it does not exist.
        /// </summary>
        public Task<bool> DeleteAsync(int number)
        {
            var station = _stations.FirstOrDefault(s => s.Number == number);
            if (station == null)
            {
                return Task.FromResult(false);
            }

            _stations.Remove(station);
            InvalidateCache();

            return Task.FromResult(true);
        }

        /// <summary>
        /// Returns summary / aggregate information.
        /// </summary>
        public Task<StationsSummaryDto> GetSummaryAsync()
        {
            var summary = new StationsSummaryDto
            {
                TotalStations = _stations.Count,
                TotalBikeStands = _stations.Sum(s => s.BikeStands),
                TotalAvailableBikes = _stations.Sum(s => s.AvailableBikes),
                OpenStations = _stations.Count(s => s.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)),
                ClosedStations = _stations.Count(s => s.Status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
            };

            return Task.FromResult(summary);
        }

        /// <summary>
        /// Used by /api/admin/seed-cosmos to push data into CosmosDb.
        /// </summary>
        public List<Station> GetAllStations()
        {
            // Defensive: return a copy to avoid external modifications
            return _stations.ToList();
        }

        /// <summary>
        /// Clears all cached query results.
        /// </summary>
        private void InvalidateCache()
        {
            // Simple approach: clear the whole memory cache for this app.
            // This is acceptable for this assignment.
            (_cache as MemoryCache)?.Compact(1.0);
        }

        public void RandomUpdateAllStations(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            lock (_stations)   // use o mesmo objeto de lock que você já usa no serviço
            {
                var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var station in _stations)
                {
                    // Mantém a capacidade (BikeStands) e só mexe na disponibilidade
                    var maxChange = Math.Max(1, station.BikeStands / 4);

                    // Variação aleatória de -maxChange até +maxChange
                    var delta = random.Next(-maxChange, maxChange + 1);

                    var newAvailable = station.AvailableBikes + delta;

                    // Garante limites 0..BikeStands
                    if (newAvailable < 0) newAvailable = 0;
                    if (newAvailable > station.BikeStands) newAvailable = station.BikeStands;

                    station.AvailableBikes       = newAvailable;
                    station.AvailableBikeStands  = station.BikeStands - station.AvailableBikes;
                    station.LastUpdateEpochMs    = nowEpoch;

                    // Pequena chance de fechar a estação, só pra simular
                    if (station.BikeStands == 0)
                    {
                        station.Status = "CLOSED";
                    }
                    else
                    {
                        // 10% de chance de CLOSED, senão OPEN
                        station.Status = random.NextDouble() < 0.1 ? "CLOSED" : "OPEN";
                    }
                }
            }
        }

    }
}
