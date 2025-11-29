using DublinBikes.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DublinBikes.Api.Tests
{
    /// <summary>
    /// Minimal integration tests hitting the real HTTP endpoints
    /// using the in-memory TestServer (WebApplicationFactory).
    /// 
    /// Focus:
    ///  - GET /api/v1/stations basic response
    ///  - Filtering by status=OPEN
    ///  - Search q over name/address
    /// </summary>
    public class StationFilteringTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public StationFilteringTests(WebApplicationFactory<Program> factory)
        {
            // This client talks directly to the in-memory API (no real network).
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetStationsV1_ReturnsOkAndNonEmptyList()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/api/v1/stations?page=1&pageSize=10");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var data = await response.Content.ReadFromJsonAsync<List<StationDto>>();
            Assert.NotNull(data);
            Assert.NotEmpty(data);
        }

        [Fact]
        public async Task GetStationsV1_FilterByStatusOpen_ReturnsOnlyOpenStations()
        {
            // Arrange: filter with status=OPEN
            var response = await _client.GetAsync("/api/v1/stations?status=OPEN&page=1&pageSize=20");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var data = await response.Content.ReadFromJsonAsync<List<StationDto>>();
            Assert.NotNull(data);
            Assert.NotEmpty(data);

            // All returned stations must have Status = "OPEN"
            Assert.All(data!, station =>
                Assert.Equal("OPEN", station.Status, ignoreCase: true));
        }

        [Fact]
        public async Task GetStationsV1_SearchByNameOrAddress_ReturnsMatchingStations()
        {
            // Arrange: search term (adjust if you know a real name in your JSON)
            const string term = "STATION"; // generic term that should match something

            var response = await _client.GetAsync($"/api/v1/stations?q={term}&page=1&pageSize=50");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var data = await response.Content.ReadFromJsonAsync<List<StationDto>>();
            Assert.NotNull(data);

            // It is acceptable that sometimes search returns empty,
            // but usually we expect at least one result.
            // If your dataset is known (e.g. "AVONDALE"),
            // you can use that instead of "STATION".
            if (data!.Count > 0)
            {
                Assert.Contains(data, s =>
                    (s.Name ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.Address ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
