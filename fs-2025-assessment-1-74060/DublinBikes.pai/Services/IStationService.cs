using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;

namespace DublinBikes.Api.Services
{
    public interface IStationService
    {
        Task<IReadOnlyList<Station>> GetStationsAsync(StationQueryParameters parameters);
        Task<Station?> GetStationByNumberAsync(int number);
        Task AddStationAsync(Station newStation);
        Task<bool> UpdateStationAsync(int number, Station updatedStation);
        Task<StationsSummaryDto> GetSummaryAsync();
    }
}
