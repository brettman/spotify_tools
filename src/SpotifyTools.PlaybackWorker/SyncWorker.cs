using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Enums;
using SpotifyTools.Sync;

namespace SpotifyTools.PlaybackWorker;

/// <summary>
/// Background service that manages incremental sync operations
/// Runs initial full sync on first run, then incremental syncs periodically
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _incrementalSyncInterval;
    private readonly bool _enableInitialFullSync;
    private readonly bool _enableIncrementalSync;
    private bool _initialFullSyncCompleted = false;

    public SyncWorker(
        IServiceProvider serviceProvider,
        ILogger<SyncWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        // Configuration with defaults
        _enableInitialFullSync = configuration.GetValue<bool>("Sync:EnableInitialFullSync", defaultValue: true);
        _enableIncrementalSync = configuration.GetValue<bool>("Sync:EnableIncrementalSync", defaultValue: true);
        
        var intervalMinutes = configuration.GetValue<int?>("Sync:IncrementalIntervalMinutes") ?? 30;
        _incrementalSyncInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync Worker starting");
        _logger.LogInformation("Initial full sync enabled: {Enabled}", _enableInitialFullSync);
        _logger.LogInformation("Incremental sync enabled: {Enabled} (interval: {Interval} minutes)", 
            _enableIncrementalSync, _incrementalSyncInterval.TotalMinutes);

        // Wait a bit before starting sync (let playback tracker start first)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Check if we need to run initial full sync
        if (_enableInitialFullSync)
        {
            try
            {
                var needsFullSync = await CheckIfFullSyncNeededAsync();
                if (needsFullSync)
                {
                    _logger.LogInformation("═══════════════════════════════════════════════════════════");
                    _logger.LogInformation("  STARTING INITIAL FULL SYNC");
                    _logger.LogInformation("═══════════════════════════════════════════════════════════");
                    _logger.LogInformation("");
                    _logger.LogInformation("This will sync your entire Spotify library.");
                    _logger.LogInformation("The process is resumable - rate limits will be handled automatically.");
                    _logger.LogInformation("You can stop and restart the service at any time.");
                    _logger.LogInformation("");

                    await RunFullSyncAsync(stoppingToken);
                    _initialFullSyncCompleted = true;

                    _logger.LogInformation("");
                    _logger.LogInformation("═══════════════════════════════════════════════════════════");
                    _logger.LogInformation("  INITIAL FULL SYNC COMPLETED");
                    _logger.LogInformation("═══════════════════════════════════════════════════════════");
                    _logger.LogInformation("");
                }
                else
                {
                    _logger.LogInformation("Initial full sync not needed - library already synced");
                    _initialFullSyncCompleted = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync cancelled during initial full sync");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial full sync failed. Will retry on next service start.");
                // Don't crash the service - we'll retry next time
            }
        }
        else
        {
            _logger.LogInformation("Initial full sync is disabled in configuration");
            _initialFullSyncCompleted = true;
        }

        // Run incremental syncs periodically
        if (_enableIncrementalSync)
        {
            _logger.LogInformation("Starting incremental sync loop (every {Interval} minutes)", 
                _incrementalSyncInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_incrementalSyncInterval, stoppingToken);

                    _logger.LogInformation("Starting incremental sync...");
                    await RunIncrementalSyncAsync(stoppingToken);
                    _logger.LogInformation("Incremental sync completed");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Sync service is shutting down");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Incremental sync failed. Will retry after next interval.");
                }
            }
        }
        else
        {
            _logger.LogInformation("Incremental sync is disabled in configuration");
        }

        _logger.LogInformation("Sync Worker stopped");
    }

    private async Task<bool> CheckIfFullSyncNeededAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Check if we have any successful full sync in history
        var allSyncs = await unitOfWork.SyncHistory.GetAllAsync();
        var hasSuccessfulFullSync = allSyncs
            .Any(s => s.SyncType == SyncType.Full && s.Status == SyncStatus.Success);

        if (hasSuccessfulFullSync)
        {
            _logger.LogInformation("Found previous successful full sync");
            return false;
        }

        // Check if there's an in-progress sync we can resume
        var inProgressSync = allSyncs
            .Where(s => s.SyncType == SyncType.Full && 
                       (s.Status == SyncStatus.InProgress || s.Status == SyncStatus.RateLimited))
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (inProgressSync != null)
        {
            _logger.LogInformation("Found in-progress sync from {StartedAt} - will resume", 
                inProgressSync.StartedAt);
            return true;
        }

        // No previous sync - need full sync
        _logger.LogInformation("No previous sync found - full sync needed");
        return true;
    }

    private async Task RunFullSyncAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IncrementalSyncOrchestrator>();

        // Subscribe to progress events
        orchestrator.ProgressChanged += (sender, args) =>
        {
            _logger.LogInformation("[{Stage}] {Message}", args.Stage, args.Message);
        };

        try
        {
            var syncHistoryId = await orchestrator.RunFullSyncAsync(stoppingToken);
            _logger.LogInformation("Full sync completed successfully (SyncHistory ID: {Id})", syncHistoryId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Full sync was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed");
            throw;
        }
    }

    private async Task RunIncrementalSyncAsync(CancellationToken stoppingToken)
    {
        // TODO: Implement incremental sync (Phase 2C)
        // For now, just run a small batch of tracks to catch new additions
        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

        try
        {
            // Sync only the first batch of tracks (most recent additions)
            var result = await syncService.SyncTracksBatchAsync(
                offset: 0,
                batchSize: 50,
                progressCallback: (current, total) => 
                    _logger.LogDebug("Incremental sync: {Current}/{Total}", current, total),
                cancellationToken: stoppingToken);

            if (result.Success)
            {
                _logger.LogInformation("Incremental sync: {New} new tracks, {Updated} updated", 
                    result.NewItemsAdded, result.ItemsUpdated);
            }
            else if (result.RateLimited)
            {
                _logger.LogWarning("Incremental sync rate limited until {ResetAt}", 
                    result.RateLimitResetAt);
            }
            else
            {
                _logger.LogError("Incremental sync failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Incremental sync error");
            // Don't throw - we'll retry on next interval
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sync Worker is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Sync Worker stopped gracefully");
    }
}
