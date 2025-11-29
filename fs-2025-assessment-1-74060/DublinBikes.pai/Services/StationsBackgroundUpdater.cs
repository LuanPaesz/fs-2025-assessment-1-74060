using Microsoft.Extensions.Hosting;

namespace DublinBikes.Api.Services
{
    /// <summary>
    /// Background service that runs alongside the API
    /// and periodically calls the FileStationService to update
    /// the stations with random values.
    /// </summary>
    public class StationsBackgroundUpdater : BackgroundService
    {
        private readonly ILogger<StationsBackgroundUpdater> _logger;
        private readonly FileStationService _fileStationService;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);

        public StationsBackgroundUpdater(
            ILogger<StationsBackgroundUpdater> logger,
            FileStationService fileStationService)
        {
            _logger = logger;
            _fileStationService = fileStationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StationsBackgroundUpdater started.");

            var random = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _fileStationService.RandomUpdateAllStations(random);
                    _logger.LogInformation("Stations updated at {Time}", DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating stations in background service");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("StationsBackgroundUpdater stopping.");
        }
    }
}
