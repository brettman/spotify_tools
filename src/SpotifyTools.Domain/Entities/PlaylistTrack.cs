namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a track within a playlist
/// </summary>
public class PlaylistTrack
{
    /// <summary>
    /// Auto-incrementing ID for this relationship
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to Playlist
    /// </summary>
    public string PlaylistId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Track
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// When this track was added to the playlist
    /// </summary>
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Spotify user ID who added this track
    /// </summary>
    public string AddedBy { get; set; } = string.Empty;

    /// <summary>
    /// Position of track in playlist (0-based)
    /// </summary>
    public int Position { get; set; }

    // Navigation properties
    public Playlist Playlist { get; set; } = null!;
    public Track Track { get; set; } = null!;
}
