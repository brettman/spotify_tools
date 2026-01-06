namespace SpotifyTools.Domain.Enums;

/// <summary>
/// Status of a sync operation
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Sync is currently in progress
    /// </summary>
    InProgress = 0,

    /// <summary>
    /// Sync completed successfully
    /// </summary>
    Success = 1,

    /// <summary>
    /// Sync failed with errors
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Sync completed but with some warnings or partial success
    /// </summary>
    Partial = 3,
    
    /// <summary>
    /// Sync was cancelled by user
    /// </summary>
    Cancelled = 4,
    
    /// <summary>
    /// Sync is paused due to rate limiting
    /// </summary>
    RateLimited = 5
}
