using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Enums;
using SpotifyTools.Sync;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Controllers;

/// <summary>
/// API endpoints for managing sync operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IncrementalSyncOrchestrator _orchestrator;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        ISyncService syncService,
        IUnitOfWork unitOfWork,
        IncrementalSyncOrchestrator orchestrator,
        ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _unitOfWork = unitOfWork;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Get current sync status (if a sync is in progress)
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SyncStatusDto>> GetStatus()
    {
        try
        {
            var status = await _orchestrator.GetCurrentSyncStatusAsync();
            
            if (status == null)
            {
                // No active sync - return idle status
                return Ok(new SyncStatusDto
                {
                    Status = "Idle",
                    IsActive = false
                });
            }

            return Ok(new SyncStatusDto
            {
                SyncHistoryId = status.SyncHistoryId,
                StartedAt = status.StartedAt,
                Status = status.Status.ToString(),
                IsActive = true,
                TracksProgress = MapPhaseProgress(status.TracksProgress),
                ArtistsProgress = MapPhaseProgress(status.ArtistsProgress),
                AlbumsProgress = MapPhaseProgress(status.AlbumsProgress),
                PlaylistsProgress = MapPhaseProgress(status.PlaylistsProgress)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status");
            return StatusCode(500, new { error = "Failed to get sync status" });
        }
    }

    /// <summary>
    /// Get sync history (last 20 syncs)
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<SyncHistoryDto>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var allHistory = await _unitOfWork.SyncHistory.GetAllAsync();
            var history = allHistory
                .OrderByDescending(h => h.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new SyncHistoryDto
                {
                    Id = h.Id,
                    SyncType = h.SyncType.ToString(),
                    StartedAt = h.StartedAt,
                    CompletedAt = h.CompletedAt,
                    Status = h.Status.ToString(),
                    ErrorMessage = h.ErrorMessage,
                    TracksAdded = h.TracksAdded,
                    TracksUpdated = h.TracksUpdated,
                    ArtistsAdded = h.ArtistsAdded,
                    AlbumsAdded = h.AlbumsAdded,
                    PlaylistsAdded = h.PlaylistsSynced,
                    DurationFormatted = FormatDuration(h.StartedAt, h.CompletedAt)
                })
                .ToList();

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync history");
            return StatusCode(500, new { error = "Failed to get sync history" });
        }
    }

    /// <summary>
    /// Start a new sync operation (Full or Incremental)
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult> StartSync([FromBody] StartSyncRequest request)
    {
        try
        {
            // Check if a sync is already running
            var currentStatus = await _orchestrator.GetCurrentSyncStatusAsync();
            if (currentStatus != null)
            {
                return BadRequest(new { error = "A sync operation is already in progress" });
            }

            // Start sync in background (don't await)
            if (request.SyncType.Equals("Full", StringComparison.OrdinalIgnoreCase))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _orchestrator.RunFullSyncAsync();
                        _logger.LogInformation("Full sync completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Full sync failed");
                    }
                });
                
                return Accepted(new { message = "Full sync started" });
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncService.IncrementalSyncAsync();
                        _logger.LogInformation("Incremental sync completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Incremental sync failed");
                    }
                });
                
                return Accepted(new { message = "Incremental sync started" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sync");
            return StatusCode(500, new { error = "Failed to start sync" });
        }
    }

    /// <summary>
    /// Get the last completed sync info
    /// </summary>
    [HttpGet("last-sync")]
    public async Task<ActionResult<SyncHistoryDto?>> GetLastSync()
    {
        try
        {
            var allHistory = await _unitOfWork.SyncHistory.GetAllAsync();
            var lastSync = allHistory
                .Where(h => h.Status == SyncStatus.Success)
                .OrderByDescending(h => h.CompletedAt)
                .FirstOrDefault();

            if (lastSync == null)
                return Ok(null);

            return Ok(new SyncHistoryDto
            {
                Id = lastSync.Id,
                SyncType = lastSync.SyncType.ToString(),
                StartedAt = lastSync.StartedAt,
                CompletedAt = lastSync.CompletedAt,
                Status = lastSync.Status.ToString(),
                ErrorMessage = lastSync.ErrorMessage,
                TracksAdded = lastSync.TracksAdded,
                TracksUpdated = lastSync.TracksUpdated,
                ArtistsAdded = lastSync.ArtistsAdded,
                AlbumsAdded = lastSync.AlbumsAdded,
                PlaylistsAdded = lastSync.PlaylistsSynced,
                DurationFormatted = FormatDuration(lastSync.StartedAt, lastSync.CompletedAt)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last sync");
            return StatusCode(500, new { error = "Failed to get last sync" });
        }
    }

    // Helper methods

    private PhaseProgressDto? MapPhaseProgress(PhaseProgress? progress)
    {
        if (progress == null)
            return null;

        return new PhaseProgressDto
        {
            Status = progress.Status.ToString(),
            CurrentOffset = progress.CurrentOffset,
            TotalItems = progress.TotalItems,
            ItemsProcessed = progress.ItemsProcessed,
            LastError = progress.LastError,
            RateLimitResetAt = progress.RateLimitResetAt,
            PercentComplete = progress.PercentComplete
        };
    }

    private string? FormatDuration(DateTime startedAt, DateTime? completedAt)
    {
        if (!completedAt.HasValue)
            return null;

        var duration = completedAt.Value - startedAt;
        
        if (duration.TotalSeconds < 60)
            return $"{duration.TotalSeconds:F0}s";
        
        if (duration.TotalMinutes < 60)
            return $"{duration.TotalMinutes:F1}m";
        
        return $"{duration.TotalHours:F1}h";
    }
}
