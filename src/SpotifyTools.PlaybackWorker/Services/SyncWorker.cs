using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpotifyTools.Sync.Services;

namespace SpotifyTools.PlaybackWorker.Services;

/// <summary>
/// Background service that runs incremental syncs alongside playback tracking
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly IncrementalSyncOrchestrator _syncOrchestrator;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(30);

    public SyncWorker(
        ILogger<SyncWorker> logger,
        IncrementalSyncOrchestrator syncOrchestrator)
    {
        _logger = logger;
        _syncOrchestrator = syncOrchestrator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker started. Will run incremental sync every {Interval}", _syncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting incremental sync cycle");
                await _syncOrchestrator.RunIncrementalSyncAsync(stoppingToken);
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
