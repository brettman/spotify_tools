namespace SpotifyTools.Domain.Entities;

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
    /// Type of entity being synced (tracks, artists, albums, playlists)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Sync phase (initial_sync, incremental_sync)
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Last successfully synced offset/index
    /// </summary>
    public int LastSyncedOffset { get; set; } = 0;

    /// <summary>
    /// Total estimated entities to sync (updated as we learn the real count)
    /// </summary>
    public int TotalEstimated { get; set; } = 0;

    /// <summary>
    /// Whether this entity type has completed syncing
    /// </summary>
    public bool IsComplete { get; set; } = false;

    /// <summary>
    /// When rate limit was hit (null if not currently rate limited)
    /// </summary>
    public DateTime? RateLimitHitAt { get; set; }

    /// <summary>
    /// When rate limit will reset (null if not rate limited)
    /// </summary>
    public DateTime? RateLimitResetAt { get; set; }

    /// <summary>
    /// Remaining API calls before rate limit (from X-RateLimit-Remaining header)
    /// </summary>
    public int? RateLimitRemaining { get; set; }

    /// <summary>
    /// When this sync state was first created
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Last time this sync state was updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// When this entity type completed syncing (null if incomplete)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
