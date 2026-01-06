namespace SpotifyTools.Sync.Models;

/// <summary>
/// Result of a batch sync operation
/// </summary>
public class BatchSyncResult
{
    /// <summary>
    /// Number of items processed in this batch
    /// </summary>
    public int ItemsProcessed { get; set; }

    /// <summary>
    /// Number of new items added to the database
    /// </summary>
    public int NewItemsAdded { get; set; }

    /// <summary>
    /// Number of existing items updated
    /// </summary>
    public int ItemsUpdated { get; set; }

    /// <summary>
    /// Whether there are more items to fetch
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// The next offset to use for pagination (if HasMore is true)
    /// </summary>
    public int NextOffset { get; set; }

    /// <summary>
    /// Whether a rate limit was encountered
    /// </summary>
    public bool RateLimited { get; set; }

    /// <summary>
    /// When the rate limit will reset (if RateLimited is true)
    /// </summary>
    public DateTime? RateLimitResetAt { get; set; }

    /// <summary>
    /// Total items estimated (if known)
    /// </summary>
    public int? TotalEstimated { get; set; }

    /// <summary>
    /// Error message if the batch failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the batch completed successfully
    /// </summary>
    public bool Success => string.IsNullOrEmpty(ErrorMessage) && !RateLimited;
}
