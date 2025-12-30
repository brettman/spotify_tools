namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Relationship between Tracks and Albums
/// Typically one-to-one, but modeled as many-to-many for flexibility
/// </summary>
public class TrackAlbum
{
    /// <summary>
    /// Foreign key to Track
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Album
    /// </summary>
    public string AlbumId { get; set; } = string.Empty;

    /// <summary>
    /// Track number on the album
    /// </summary>
    public int TrackNumber { get; set; }

    /// <summary>
    /// Disc number (for multi-disc albums)
    /// </summary>
    public int DiscNumber { get; set; }

    // Navigation properties
    public Track Track { get; set; } = null!;
    public Album Album { get; set; } = null!;
}
