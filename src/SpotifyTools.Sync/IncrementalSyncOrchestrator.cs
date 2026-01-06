using Microsoft.Extensions.Logging;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Domain.Enums;
using SpotifyTools.Sync.Models;
using SpotifyTools.Sync.Services;

namespace SpotifyTools.Sync;

/// <summary>
/// Orchestrates incremental sync operations with checkpointing and rate limit handling.
/// Allows syncs to be paused/resumed across app restarts.
/// </summary>
public class IncrementalSyncOrchestrator
{
    private readonly ISyncService _syncService;
    private readonly ISyncStateRepository _syncStateRepository;
    private readonly IRateLimitTracker _rateLimitTracker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IncrementalSyncOrchestrator> _logger;

    // Configuration
    private const int TRACKS_BATCH_SIZE = 50;  // Spotify API limit for saved tracks
    private const int ARTISTS_BATCH_SIZE = 100; // We fetch 50 at a time from API
    private const int ALBUMS_BATCH_SIZE = 100;  // We fetch 20 at a time from API
    private const int PLAYLISTS_BATCH_SIZE = 50; // Spotify API limit

    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

    public IncrementalSyncOrchestrator(
        ISyncService syncService,
        ISyncStateRepository syncStateRepository,
        IRateLimitTracker rateLimitTracker,
        IUnitOfWork unitOfWork,
        ILogger<IncrementalSyncOrchestrator> logger)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _syncStateRepository = syncStateRepository ?? throw new ArgumentNullException(nameof(syncStateRepository));
        _rateLimitTracker = rateLimitTracker ?? throw new ArgumentNullException(nameof(rateLimitTracker));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs a full sync from scratch or resumes from last checkpoint.
    /// Handles rate limits by waiting and resuming automatically.
    /// </summary>
    public async Task<int> RunFullSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting incremental full sync orchestration");

        // Create or resume sync history
        var syncHistory = new SyncHistory
        {
            SyncType = SyncType.Full,
            StartedAt = DateTime.UtcNow,
            Status = SyncStatus.InProgress
        };
        await _unitOfWork.SyncHistory.AddAsync(syncHistory);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            // Phase 1: Sync Tracks (creates stub artists/albums)
            await SyncTracksPhaseAsync(syncHistory.Id, cancellationToken);

            // Phase 2: Enrich Artists
            await SyncArtistsPhaseAsync(syncHistory.Id, cancellationToken);

            // Phase 3: Enrich Albums
            await SyncAlbumsPhaseAsync(syncHistory.Id, cancellationToken);

            // Phase 4: Sync Playlists
            await SyncPlaylistsPhaseAsync(syncHistory.Id, cancellationToken);

            // Mark sync as complete
            syncHistory.Status = SyncStatus.Success;
            syncHistory.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Full sync completed successfully");
            OnProgressChanged("Complete", 1, 1, "Full sync completed!");

            return syncHistory.Id;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sync cancelled by user");
            syncHistory.Status = SyncStatus.Cancelled;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.ErrorMessage = "Cancelled by user";
            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed");
            syncHistory.Status = SyncStatus.Failed;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.ErrorMessage = ex.Message;
            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();
            throw;
        }
    }

    private async Task SyncTracksPhaseAsync(int syncHistoryId, CancellationToken cancellationToken)
    {
        var stateKey = $"sync_{syncHistoryId}_tracks";
        var state = await _syncStateRepository.GetByKeyAsync(stateKey) ?? new SyncState
        {
            StateKey = stateKey,
            EntityType = "tracks",
            CurrentOffset = 0,
            Status = SyncStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        if (state.Id == 0)
        {
            await _syncStateRepository.AddAsync(state);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Syncing tracks from offset {Offset}", state.CurrentOffset);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _syncService.SyncTracksBatchAsync(
                state.CurrentOffset,
                TRACKS_BATCH_SIZE,
                (current, total) => OnProgressChanged("Tracks", current, total, $"Syncing tracks: {current}/{total}"),
                cancellationToken);

            if (result.RateLimited)
            {
                _logger.LogWarning("Rate limit hit. Pausing until {ResetAt}", result.RateLimitResetAt);
                state.Status = SyncStatus.RateLimited;
                state.RateLimitResetAt = result.RateLimitResetAt;
                state.LastError = "Rate limit hit";
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();

                OnProgressChanged("Tracks", state.CurrentOffset, state.TotalItems ?? 0, 
                    $"Rate limited. Will resume after {result.RateLimitResetAt:HH:mm}");

                // Wait until rate limit resets
                var waitTime = (result.RateLimitResetAt ?? DateTime.UtcNow.AddHours(24)) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                continue;
            }

            if (!result.Success)
            {
                _logger.LogError("Tracks batch failed: {Error}", result.ErrorMessage);
                state.Status = SyncStatus.Failed;
                state.LastError = result.ErrorMessage;
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                throw new Exception($"Tracks sync failed: {result.ErrorMessage}");
            }

            // Update checkpoint
            state.CurrentOffset = result.NextOffset;
            state.TotalItems = result.TotalEstimated;
            state.ItemsProcessed += result.ItemsProcessed;
            state.UpdatedAt = DateTime.UtcNow;
            state.Status = SyncStatus.InProgress;
            await _syncStateRepository.UpdateAsync(state);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Tracks batch complete: offset {Offset}, processed {Processed}, new {New}, updated {Updated}",
                result.NextOffset, result.ItemsProcessed, result.NewItemsAdded, result.ItemsUpdated);

            if (!result.HasMore)
            {
                state.Status = SyncStatus.Success;
                state.CompletedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Tracks phase completed. Total processed: {Total}", state.ItemsProcessed);
                break;
            }
        }
    }

    private async Task SyncArtistsPhaseAsync(int syncHistoryId, CancellationToken cancellationToken)
    {
        var stateKey = $"sync_{syncHistoryId}_artists";
        var state = await _syncStateRepository.GetByKeyAsync(stateKey) ?? new SyncState
        {
            StateKey = stateKey,
            EntityType = "artists",
            CurrentOffset = 0,
            Status = SyncStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        if (state.Id == 0)
        {
            await _syncStateRepository.AddAsync(state);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Enriching artists from offset {Offset}", state.CurrentOffset);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _syncService.SyncArtistsBatchAsync(
                state.CurrentOffset,
                ARTISTS_BATCH_SIZE,
                (current, total) => OnProgressChanged("Artists", current, total, $"Enriching artists: {current}/{total}"),
                cancellationToken);

            if (result.RateLimited)
            {
                _logger.LogWarning("Rate limit hit during artists sync");
                state.Status = SyncStatus.RateLimited;
                state.RateLimitResetAt = result.RateLimitResetAt;
                state.LastError = "Rate limit hit";
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();

                OnProgressChanged("Artists", state.CurrentOffset, state.TotalItems ?? 0,
                    $"Rate limited. Will resume after {result.RateLimitResetAt:HH:mm}");

                var waitTime = (result.RateLimitResetAt ?? DateTime.UtcNow.AddHours(24)) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                continue;
            }

            if (!result.Success)
            {
                _logger.LogError("Artists batch failed: {Error}", result.ErrorMessage);
                state.Status = SyncStatus.Failed;
                state.LastError = result.ErrorMessage;
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                throw new Exception($"Artists sync failed: {result.ErrorMessage}");
            }

            state.CurrentOffset = result.NextOffset;
            state.TotalItems = result.TotalEstimated;
            state.ItemsProcessed += result.ItemsProcessed;
            state.UpdatedAt = DateTime.UtcNow;
            await _syncStateRepository.UpdateAsync(state);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Artists batch complete: offset {Offset}, enriched {Enriched}",
                result.NextOffset, result.ItemsUpdated);

            if (!result.HasMore)
            {
                state.Status = SyncStatus.Success;
                state.CompletedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Artists phase completed. Total enriched: {Total}", state.ItemsProcessed);
                break;
            }
        }
    }

    private async Task SyncAlbumsPhaseAsync(int syncHistoryId, CancellationToken cancellationToken)
    {
        var stateKey = $"sync_{syncHistoryId}_albums";
        var state = await _syncStateRepository.GetByKeyAsync(stateKey) ?? new SyncState
        {
            StateKey = stateKey,
            EntityType = "albums",
            CurrentOffset = 0,
            Status = SyncStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        if (state.Id == 0)
        {
            await _syncStateRepository.AddAsync(state);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Enriching albums from offset {Offset}", state.CurrentOffset);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _syncService.SyncAlbumsBatchAsync(
                state.CurrentOffset,
                ALBUMS_BATCH_SIZE,
                (current, total) => OnProgressChanged("Albums", current, total, $"Enriching albums: {current}/{total}"),
                cancellationToken);

            if (result.RateLimited)
            {
                _logger.LogWarning("Rate limit hit during albums sync");
                state.Status = SyncStatus.RateLimited;
                state.RateLimitResetAt = result.RateLimitResetAt;
                state.LastError = "Rate limit hit";
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();

                OnProgressChanged("Albums", state.CurrentOffset, state.TotalItems ?? 0,
                    $"Rate limited. Will resume after {result.RateLimitResetAt:HH:mm}");

                var waitTime = (result.RateLimitResetAt ?? DateTime.UtcNow.AddHours(24)) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                continue;
            }

            if (!result.Success)
            {
                _logger.LogError("Albums batch failed: {Error}", result.ErrorMessage);
                state.Status = SyncStatus.Failed;
                state.LastError = result.ErrorMessage;
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                throw new Exception($"Albums sync failed: {result.ErrorMessage}");
            }

            state.CurrentOffset = result.NextOffset;
            state.TotalItems = result.TotalEstimated;
            state.ItemsProcessed += result.ItemsProcessed;
            state.UpdatedAt = DateTime.UtcNow;
            await _syncStateRepository.UpdateAsync(state);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Albums batch complete: offset {Offset}, enriched {Enriched}",
                result.NextOffset, result.ItemsUpdated);

            if (!result.HasMore)
            {
                state.Status = SyncStatus.Success;
                state.CompletedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Albums phase completed. Total enriched: {Total}", state.ItemsProcessed);
                break;
            }
        }
    }

    private async Task SyncPlaylistsPhaseAsync(int syncHistoryId, CancellationToken cancellationToken)
    {
        var stateKey = $"sync_{syncHistoryId}_playlists";
        var state = await _syncStateRepository.GetByKeyAsync(stateKey) ?? new SyncState
        {
            StateKey = stateKey,
            EntityType = "playlists",
            CurrentOffset = 0,
            Status = SyncStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        if (state.Id == 0)
        {
            await _syncStateRepository.AddAsync(state);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Syncing playlists from offset {Offset}", state.CurrentOffset);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _syncService.SyncPlaylistsBatchAsync(
                state.CurrentOffset,
                PLAYLISTS_BATCH_SIZE,
                (current, total) => OnProgressChanged("Playlists", current, total, $"Syncing playlists: {current}/{total}"),
                cancellationToken);

            if (result.RateLimited)
            {
                _logger.LogWarning("Rate limit hit during playlists sync");
                state.Status = SyncStatus.RateLimited;
                state.RateLimitResetAt = result.RateLimitResetAt;
                state.LastError = "Rate limit hit";
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();

                OnProgressChanged("Playlists", state.CurrentOffset, state.TotalItems ?? 0,
                    $"Rate limited. Will resume after {result.RateLimitResetAt:HH:mm}");

                var waitTime = (result.RateLimitResetAt ?? DateTime.UtcNow.AddHours(24)) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                continue;
            }

            if (!result.Success)
            {
                _logger.LogError("Playlists batch failed: {Error}", result.ErrorMessage);
                state.Status = SyncStatus.Failed;
                state.LastError = result.ErrorMessage;
                state.UpdatedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                throw new Exception($"Playlists sync failed: {result.ErrorMessage}");
            }

            state.CurrentOffset = result.NextOffset;
            state.TotalItems = result.TotalEstimated;
            state.ItemsProcessed += result.ItemsProcessed;
            state.UpdatedAt = DateTime.UtcNow;
            await _syncStateRepository.UpdateAsync(state);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Playlists batch complete: offset {Offset}, new {New}, updated {Updated}",
                result.NextOffset, result.NewItemsAdded, result.ItemsUpdated);

            if (!result.HasMore)
            {
                state.Status = SyncStatus.Success;
                state.CompletedAt = DateTime.UtcNow;
                await _syncStateRepository.UpdateAsync(state);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Playlists phase completed. Total processed: {Total}", state.ItemsProcessed);
                break;
            }
        }
    }

    /// <summary>
    /// Gets the current sync status for display in UI
    /// </summary>
    public async Task<SyncStatusSummary?> GetCurrentSyncStatusAsync()
    {
        // Find the most recent in-progress sync
        var allSyncs = await _unitOfWork.SyncHistory.GetAllAsync();
        var activeSync = allSyncs
            .Where(s => s.Status == SyncStatus.InProgress || s.Status == SyncStatus.RateLimited)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (activeSync == null)
            return null;

        // Get state for all phases
        var tracksState = await _syncStateRepository.GetByKeyAsync($"sync_{activeSync.Id}_tracks");
        var artistsState = await _syncStateRepository.GetByKeyAsync($"sync_{activeSync.Id}_artists");
        var albumsState = await _syncStateRepository.GetByKeyAsync($"sync_{activeSync.Id}_albums");
        var playlistsState = await _syncStateRepository.GetByKeyAsync($"sync_{activeSync.Id}_playlists");

        return new SyncStatusSummary
        {
            SyncHistoryId = activeSync.Id,
            StartedAt = activeSync.StartedAt,
            Status = activeSync.Status,
            TracksProgress = CreatePhaseProgress(tracksState),
            ArtistsProgress = CreatePhaseProgress(artistsState),
            AlbumsProgress = CreatePhaseProgress(albumsState),
            PlaylistsProgress = CreatePhaseProgress(playlistsState)
        };
    }

    private PhaseProgress? CreatePhaseProgress(SyncState? state)
    {
        if (state == null)
            return null;

        return new PhaseProgress
        {
            Status = state.Status,
            CurrentOffset = state.CurrentOffset,
            TotalItems = state.TotalItems,
            ItemsProcessed = state.ItemsProcessed,
            LastError = state.LastError,
            RateLimitResetAt = state.RateLimitResetAt
        };
    }

    private void OnProgressChanged(string stage, int current, int total, string message)
    {
        ProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            Stage = stage,
            Current = current,
            Total = total,
            Message = message
        });

        _logger.LogInformation("[{Stage}] {Message}", stage, message);
    }
}

/// <summary>
/// Summary of current sync operation status
/// </summary>
public class SyncStatusSummary
{
    public int SyncHistoryId { get; set; }
    public DateTime StartedAt { get; set; }
    public SyncStatus Status { get; set; }
    public PhaseProgress? TracksProgress { get; set; }
    public PhaseProgress? ArtistsProgress { get; set; }
    public PhaseProgress? AlbumsProgress { get; set; }
    public PhaseProgress? PlaylistsProgress { get; set; }
}

/// <summary>
/// Progress of a single sync phase
/// </summary>
public class PhaseProgress
{
    public SyncStatus Status { get; set; }
    public int CurrentOffset { get; set; }
    public int? TotalItems { get; set; }
    public int ItemsProcessed { get; set; }
    public string? LastError { get; set; }
    public DateTime? RateLimitResetAt { get; set; }

    public int PercentComplete => TotalItems.HasValue && TotalItems.Value > 0
        ? (int)((CurrentOffset / (double)TotalItems.Value) * 100)
        : 0;
}
