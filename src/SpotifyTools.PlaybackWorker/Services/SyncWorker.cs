using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpotifyTools.Sync;

namespace SpotifyTools.PlaybackWorker.Services;

/// <summary>
/// Background service that runs incremental syncs alongside playback tracking
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(30);

    public SyncWorker(
        ILogger<SyncWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker started. Will run incremental sync every {Interval}", _syncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting incremental sync cycle");
                
                // Create scope to resolve scoped services
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                
                await syncService.IncrementalSyncAsync(stoppingToken);
                
                _logger.LogInformation("Incremental sync cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during incremental sync cycle");
            }

            // Wait before next sync cycle
            _logger.LogInformation("Next sync in {Interval}", _syncInterval);
            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("SyncWorker stopped");
    }
}
