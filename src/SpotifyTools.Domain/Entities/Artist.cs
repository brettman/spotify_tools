namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a Spotify artist
/// </summary>
public class Artist
{
    /// <summary>
    /// Spotify artist ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Artist name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Spotify popularity score (0-100)
    /// </summary>
    public int Popularity { get; set; }

    /// <summary>
    /// Number of followers on Spotify
    /// </summary>
    public int Followers { get; set; }

    /// <summary>
    /// Array of genre tags associated with this artist
    /// </summary>
    public string[] Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// URL to artist image/photo
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// When this artist was first imported into our database
    /// </summary>
    public DateTime FirstSyncedAt { get; set; }

    /// <summary>
    /// When this artist was last updated in our database
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    // Navigation properties
    public ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();
}
