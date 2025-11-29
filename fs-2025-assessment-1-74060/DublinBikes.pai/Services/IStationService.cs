using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;

namespace DublinBikes.Api.Services
{
    /// <summary>
    /// Abstraction for station data access.
    /// V1 (file) and V2 (Cosmos) both implement this.
    /// </summary>
    public interface IStationService
    {
        Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters);
        Task<Station?> GetStationByNumberAsync(int number);
        Task AddStationAsync(Station newStation);
        Task<bool> UpdateStationAsync(int number, Station updatedStation);
        Task<StationsSummaryDto> GetSummaryAsync();

        // New: delete by station number
        Task<bool> DeleteAsync(int number);
    }
}
