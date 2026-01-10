namespace SpotifyTools.Web.DTOs;

/// <summary>
/// Current sync operation status
/// </summary>
public class SyncStatusDto
{
    public int? SyncHistoryId { get; set; }
    public DateTime? StartedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public PhaseProgressDto? TracksProgress { get; set; }
    public PhaseProgressDto? ArtistsProgress { get; set; }
    public PhaseProgressDto? AlbumsProgress { get; set; }
    public PhaseProgressDto? PlaylistsProgress { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Progress of a single sync phase
/// </summary>
public class PhaseProgressDto
{
    public string Status { get; set; } = string.Empty;
    public int CurrentOffset { get; set; }
    public int? TotalItems { get; set; }
    public int ItemsProcessed { get; set; }
    public string? LastError { get; set; }
    public DateTime? RateLimitResetAt { get; set; }
    public int PercentComplete { get; set; }
}

/// <summary>
/// Sync history record
/// </summary>
public class SyncHistoryDto
{
    public int Id { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int? TracksAdded { get; set; }
    public int? TracksUpdated { get; set; }
    public int? ArtistsAdded { get; set; }
    public int? AlbumsAdded { get; set; }
    public int? PlaylistsAdded { get; set; }
    public string? DurationFormatted { get; set; }
}

/// <summary>
/// Request to start a sync operation
/// </summary>
public class StartSyncRequest
{
    public string SyncType { get; set; } = "Incremental"; // "Full" or "Incremental"
}
