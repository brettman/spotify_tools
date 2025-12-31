namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents Spotify audio analysis metadata for a track
/// This provides section-by-section breakdown of musical characteristics
/// Particularly useful for progressive rock and jazz with changing keys/tempos
/// </summary>
public class AudioAnalysis
{
    /// <summary>
    /// Spotify track ID (foreign key to Track)
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Overall estimated tempo in beats per minute (BPM) for the whole track
    /// </summary>
    public float TrackTempo { get; set; }

    /// <summary>
    /// Overall key the track is in (0-11, -1 for no detection)
    /// 0 = C, 1 = C♯/D♭, 2 = D, ..., 11 = B
    /// </summary>
    public int TrackKey { get; set; }

    /// <summary>
    /// Overall modality (major or minor) for the whole track
    /// 0 = Minor, 1 = Major, -1 = No result
    /// </summary>
    public int TrackMode { get; set; }

    /// <summary>
    /// Overall time signature for the whole track
    /// </summary>
    public int TrackTimeSignature { get; set; }

    /// <summary>
    /// Overall loudness in decibels (dB)
    /// </summary>
    public float TrackLoudness { get; set; }

    /// <summary>
    /// Track duration in seconds
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// When this analysis was fetched from Spotify
    /// </summary>
    public DateTime FetchedAt { get; set; }

    // Navigation properties
    public Track Track { get; set; } = null!;
    public ICollection<AudioAnalysisSection> Sections { get; set; } = new List<AudioAnalysisSection>();
}
