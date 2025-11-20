using System.Text.Json;
using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DublinBikes.Api.Services
{
    /// <summary>
    /// Serviço que carrega dados do dublinbike.json no startup
    /// e mantém tudo em memória. Implementa filtros, busca, sort, paginação e summary.
    /// Também expõe um método para atualizações aleatórias (usado pelo background service).
    /// </summary>
    public class FileStationService : IStationService
    {
        private readonly List<Station> _stations;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FileStationService> _logger;

        private const string CachePrefix = "stations_query_";

        public FileStationService(
            IWebHostEnvironment env,
            IMemoryCache cache,
            ILogger<FileStationService> logger)
        {
            _cache = cache;
            _logger = logger;

            // Caminho físico do JSON: <root do projeto>/Data/dublinbike.json
            var dataPath = Path.Combine(env.ContentRootPath, "Data", "dublinbike.json");

            if (!File.Exists(dataPath))
            {
                throw new FileNotFoundException("dublinbike.json not found", dataPath);
            }

            var json = File.ReadAllText(dataPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var stations = JsonSerializer.Deserialize<List<Station>>(json, options);
            _stations = stations ?? new List<Station>();

            _logger.LogInformation("Loaded {Count} stations from {Path}", _stations.Count, dataPath);
        }

        public Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters)
        {
            // Monta uma chave de cache baseada nos filtros
            var cacheKey = CachePrefix +
                           $"{parameters.Status}_{parameters.MinBikes}_{parameters.SearchTerm}_{parameters.Sort}_{parameters.Dir}_{parameters.Page}_{parameters.PageSize}";

            // Se já temos resultado em cache, devolve direto
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Station>? cached))
            {
                return Task.FromResult(cached!);
            }

            IEnumerable<Station> query = _stations;

            // Filtro: status=OPEN|CLOSED
            if (!string.IsNullOrWhiteSpace(parameters.Status))
            {
                var status = parameters.Status.Trim();
                query = query.Where(s =>
                    s.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Filtro: minBikes
            if (parameters.MinBikes.HasValue)
            {
                query = query.Where(s => s.AvailableBikes >= parameters.MinBikes.Value);
            }

            // Busca q em name/address
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim();
                query = query.Where(s =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Sorting: name | availableBikes | occupancy
            query = ApplySorting(query, parameters.Sort, parameters.Dir);

            // Paging
            var page = parameters.Page <= 0 ? 1 : parameters.Page;
            var pageSize = parameters.PageSize <= 0 ? 20 : parameters.PageSize;

            query = query.Skip((page - 1) * pageSize).Take(pageSize);

            var result = query.ToList().AsReadOnly();

            // Cache por 5 minutos
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return Task.FromResult((IReadOnlyList<Station>)result);
        }

        private static IEnumerable<Station> ApplySorting(
            IEnumerable<Station> query,
            string sort,
            string dir)
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

        public Task<Station?> GetStationByNumberAsync(int number)
        {
            var station = _stations.FirstOrDefault(s => s.Number == number);
            return Task.FromResult(station);
        }

        public Task AddStationAsync(Station newStation)
        {
            if (_stations.Any(s => s.Number == newStation.Number))
            {
                throw new InvalidOperationException($"Station {newStation.Number} already exists.");
            }

            _stations.Add(newStation);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateStationAsync(int number, Station updatedStation)
        {
            var existing = _stations.FirstOrDefault(s => s.Number == number);
            if (existing == null)
            {
                return Task.FromResult(false);
            }

            existing.Name = updatedStation.Name;
            existing.Address = updatedStation.Address;
            existing.Position = updatedStation.Position;
            existing.BikeStands = updatedStation.BikeStands;
            existing.AvailableBikes = updatedStation.AvailableBikes;
            existing.AvailableBikeStands = updatedStation.AvailableBikeStands;
            existing.Status = updatedStation.Status;
            existing.LastUpdateEpochMs = updatedStation.LastUpdateEpochMs;

            return Task.FromResult(true);
        }

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
        /// Método chamado pelo background service para atualizar capacidade e disponibilidade
        /// de forma aleatória, simulando um feed ao vivo.
        /// </summary>
        public void RandomUpdateAllStations(Random random)
        {
            foreach (var s in _stations)
            {
                var newCapacity = random.Next(5, 50);     // capacidade total
                var newAvailable = random.Next(0, newCapacity);

                s.BikeStands = newCapacity;
                s.AvailableBikes = newAvailable;
                s.AvailableBikeStands = newCapacity - newAvailable;
                s.LastUpdateEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
        public IReadOnlyList<Station> GetAllStations() => _stations.AsReadOnly();

    }
}
