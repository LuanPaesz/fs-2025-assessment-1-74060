using System.Net.Http.Json;
using System.Web;
using DublinBikes.Blazor.Models;

namespace DublinBikes.Blazor.Services
{
    /// <summary>
    /// Cliente para chamar a API DublinBikes (V2 - Cosmos).
    /// Ele recebe um HttpClient configurado com BaseUrl no Program.cs.
    /// </summary>
    public class StationsApiClient
    {
        private readonly HttpClient _httpClient;

        public StationsApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // GET /api/v2/stations com filtros/paging
        public async Task<IReadOnlyList<StationDto>> GetStationsAsync(StationQueryParameters parameters, CancellationToken ct = default)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            if (!string.IsNullOrWhiteSpace(parameters.Status))
                query["status"] = parameters.Status;

            if (parameters.MinBikes.HasValue)
                query["minBikes"] = parameters.MinBikes.Value.ToString();

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
                query["q"] = parameters.SearchTerm;

            query["sort"] = parameters.Sort;
            query["dir"] = parameters.Dir;
            query["page"] = parameters.Page.ToString();
            query["pageSize"] = parameters.PageSize.ToString();

            var uriBuilder = new UriBuilder(new Uri(_httpClient.BaseAddress!, "/api/v2/stations"))
            {
                Query = query.ToString()
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Aqui você pode logar ou lançar exceção mais amigável
                throw new HttpRequestException($"Erro ao carregar estações: {response.StatusCode}");
            }

            var data = await response.Content.ReadFromJsonAsync<List<StationDto>>(cancellationToken: ct);
            return data ?? new List<StationDto>();
        }

        // GET /api/v2/stations/{number}
        public async Task<StationDto?> GetStationAsync(int number, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/api/v2/stations/{number}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<StationDto>(cancellationToken: ct);
        }

        // GET /api/v2/stations/summary
        public async Task<StationsSummaryDto?> GetSummaryAsync(CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync("/api/v2/stations/summary", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<StationsSummaryDto>(cancellationToken: ct);
        }

        // POST /api/v2/stations
        public async Task<StationDto?> CreateStationAsync(StationDto station, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v2/stations", station, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Erro ao criar estação: {response.StatusCode}");
            }
            return await response.Content.ReadFromJsonAsync<StationDto>(cancellationToken: ct);
        }

        // PUT /api/v2/stations/{number}
        public async Task<bool> UpdateStationAsync(int number, StationDto station, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/v2/stations/{number}", station, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();
            return true;
        }

        // DELETE /api/v2/stations/{number}
        public async Task<bool> DeleteStationAsync(int number, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"/api/v2/stations/{number}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();
            return true;
        }
    }
}
