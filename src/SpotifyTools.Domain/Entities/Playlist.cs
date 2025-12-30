namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a Spotify playlist
/// </summary>
public class Playlist
{
    /// <summary>
    /// Spotify playlist ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Playlist name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Playlist description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Spotify user ID of the playlist owner
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the playlist is public
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Spotify snapshot ID - used to detect changes
    /// </summary>
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// When this playlist was first imported into our database
    /// </summary>
    public DateTime FirstSyncedAt { get; set; }

    /// <summary>
    /// When this playlist was last updated in our database
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    // Navigation properties
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
