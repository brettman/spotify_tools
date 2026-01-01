using SpotifyTools.Domain.Enums;

namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Tracks the history of sync operations
/// </summary>
public class SyncHistory
{
    /// <summary>
    /// Auto-incrementing ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Type of sync operation
    /// </summary>
    public SyncType SyncType { get; set; }

    /// <summary>
    /// When the sync started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the sync completed (null if still running or failed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Number of tracks added during this sync
    /// </summary>
    public int TracksAdded { get; set; }

    /// <summary>
    /// Number of tracks updated during this sync
    /// </summary>
    public int TracksUpdated { get; set; }

    /// <summary>
    /// Number of artists added during this sync
    /// </summary>
    public int ArtistsAdded { get; set; }

    /// <summary>
    /// Number of albums added during this sync
    /// </summary>
    public int AlbumsAdded { get; set; }

    /// <summary>
    /// Number of playlists synced
    /// </summary>
    public int PlaylistsSynced { get; set; }

    /// <summary>
    /// Track IDs found in playlists but not in saved library (JSON array)
    /// </summary>
    public string? MissingPlaylistTrackIds { get; set; }

    /// <summary>
    /// Status of the sync operation
    /// </summary>
    public SyncStatus Status { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Spotify user ID who initiated the sync
    /// </summary>
    public string? UserIdentifier { get; set; }
}
