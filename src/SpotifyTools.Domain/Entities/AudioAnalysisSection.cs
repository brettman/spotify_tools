namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a section within a track's audio analysis
/// Sections are large structural divisions like verse, chorus, bridge
/// Each section can have different key, tempo, and time signature
/// </summary>
public class AudioAnalysisSection
{
    /// <summary>
    /// Auto-increment primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Spotify track ID (foreign key to AudioAnalysis)
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Start time of the section in seconds
    /// </summary>
    public float Start { get; set; }

    /// <summary>
    /// Duration of the section in seconds
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// Confidence that the section detection is correct (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Overall loudness of the section in decibels (dB)
    /// </summary>
    public float Loudness { get; set; }

    /// <summary>
    /// Estimated tempo for this section in BPM
    /// Key for detecting tempo changes in progressive rock
    /// </summary>
    public float Tempo { get; set; }

    /// <summary>
    /// Confidence that the tempo detection is correct (0.0 to 1.0)
    /// </summary>
    public float TempoConfidence { get; set; }

    /// <summary>
    /// Key of this section (0-11, -1 for no detection)
    /// 0 = C, 1 = C♯/D♭, 2 = D, ..., 11 = B
    /// Key for detecting key changes in progressive rock/jazz
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// Confidence that the key detection is correct (0.0 to 1.0)
    /// </summary>
    public float KeyConfidence { get; set; }

    /// <summary>
    /// Modality of this section (major or minor)
    /// 0 = Minor, 1 = Major, -1 = No result
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// Confidence that the mode detection is correct (0.0 to 1.0)
    /// </summary>
    public float ModeConfidence { get; set; }

    /// <summary>
    /// Time signature of this section (3-7)
    /// Key for detecting time signature changes in progressive rock
    /// </summary>
    public int TimeSignature { get; set; }

    /// <summary>
    /// Confidence that the time signature detection is correct (0.0 to 1.0)
    /// </summary>
    public float TimeSignatureConfidence { get; set; }

    // Navigation property
    public AudioAnalysis AudioAnalysis { get; set; } = null!;
}
