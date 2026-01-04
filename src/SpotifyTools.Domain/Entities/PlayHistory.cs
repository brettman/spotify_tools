namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Tracks individual play events from Spotify listening history
/// </summary>
public class PlayHistory
{
    /// <summary>
    /// Unique identifier for this play event (generated)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Spotify track ID that was played
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// When the track was played (from Spotify)
    /// </summary>
    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Context type where the track was played (playlist, album, artist, collection, search)
    /// </summary>
    public string? ContextType { get; set; }

    /// <summary>
    /// Spotify URI of the context (e.g., spotify:playlist:xxxxx)
    /// </summary>
    public string? ContextUri { get; set; }

    /// <summary>
    /// When this play event was recorded in our database
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Track Track { get; set; } = null!;
}
