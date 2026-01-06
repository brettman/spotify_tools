namespace SpotifyTools.Domain.Entities;

using SpotifyTools.Domain.Enums;

/// <summary>
/// Tracks the state of ongoing sync operations with resumable checkpoints
/// </summary>
public class SyncState
{
    /// <summary>
    /// Auto-incrementing ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique state key (e.g., "sync_123_tracks")
    /// </summary>
    public string StateKey { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity being synced (tracks, artists, albums, playlists)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Current pagination offset
    /// </summary>
    public int CurrentOffset { get; set; } = 0;

    /// <summary>
    /// Total estimated entities to sync (updated as we learn the real count)
    /// </summary>
    public int? TotalItems { get; set; }

    /// <summary>
    /// Number of items processed so far
    /// </summary>
    public int ItemsProcessed { get; set; } = 0;

    /// <summary>
    /// Current status of this sync phase
    /// </summary>
    public SyncStatus Status { get; set; } = SyncStatus.InProgress;

    /// <summary>
    /// Last error message if sync failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When rate limit will reset (null if not rate limited)
    /// </summary>
    public DateTime? RateLimitResetAt { get; set; }

    /// <summary>
    /// When this sync state was first created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last time this sync state was updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When this entity type completed syncing (null if incomplete)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
