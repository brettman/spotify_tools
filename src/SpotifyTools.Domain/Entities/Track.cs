namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a Spotify track
/// </summary>
public class Track
{
    /// <summary>
    /// Spotify track ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Track name/title
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Whether the track has explicit lyrics
    /// </summary>
    public bool Explicit { get; set; }

    /// <summary>
    /// Spotify popularity score (0-100)
    /// </summary>
    public int Popularity { get; set; }

    /// <summary>
    /// International Standard Recording Code
    /// </summary>
    public string? Isrc { get; set; }

    /// <summary>
    /// When the user added this track to their library
    /// </summary>
    public DateTime? AddedAt { get; set; }

    /// <summary>
    /// When this track was first imported into our database
    /// </summary>
    public DateTime FirstSyncedAt { get; set; }

    /// <summary>
    /// When this track was last updated in our database
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    /// <summary>
    /// Extended metadata from external sources (stored as JSONB in PostgreSQL)
    /// </summary>
    public Dictionary<string, object>? ExtendedMetadata { get; set; }

    // Navigation properties
    public AudioFeatures? AudioFeatures { get; set; }
    public AudioAnalysis? AudioAnalysis { get; set; }
    public ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();
    public ICollection<TrackAlbum> TrackAlbums { get; set; } = new List<TrackAlbum>();
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
