namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Many-to-many relationship between Tracks and Artists
/// </summary>
public class TrackArtist
{
    /// <summary>
    /// Foreign key to Track
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Artist
    /// </summary>
    public string ArtistId { get; set; } = string.Empty;

    /// <summary>
    /// Position of this artist in the track's artist list (0-based)
    /// </summary>
    public int Position { get; set; }

    // Navigation properties
    public Track Track { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
}
