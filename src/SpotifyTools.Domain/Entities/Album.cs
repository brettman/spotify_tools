namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a Spotify album
/// </summary>
public class Album
{
    /// <summary>
    /// Spotify album ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Album name/title
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Album type (album, single, compilation)
    /// </summary>
    public string AlbumType { get; set; } = string.Empty;

    /// <summary>
    /// Album release date
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Total number of tracks on the album
    /// </summary>
    public int TotalTracks { get; set; }

    /// <summary>
    /// Record label
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// URL to album cover art
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// When this album was first imported into our database
    /// </summary>
    public DateTime FirstSyncedAt { get; set; }

    /// <summary>
    /// When this album was last updated in our database
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    // Navigation properties
    public ICollection<TrackAlbum> TrackAlbums { get; set; } = new List<TrackAlbum>();
}
