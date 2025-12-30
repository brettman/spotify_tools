namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents Spotify audio features for a track
/// This is the KEY entity for audio analysis and analytics
/// </summary>
public class AudioFeatures
{
    /// <summary>
    /// Spotify track ID (foreign key to Track)
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// A confidence measure from 0.0 to 1.0 of whether the track is acoustic
    /// </summary>
    public float Acousticness { get; set; }

    /// <summary>
    /// How suitable a track is for dancing (0.0 to 1.0)
    /// Based on tempo, rhythm stability, beat strength, and regularity
    /// </summary>
    public float Danceability { get; set; }

    /// <summary>
    /// Perceptual measure of intensity and activity (0.0 to 1.0)
    /// Energetic tracks feel fast, loud, and noisy
    /// </summary>
    public float Energy { get; set; }

    /// <summary>
    /// Predicts whether a track contains no vocals (0.0 to 1.0)
    /// Values above 0.5 represent instrumental tracks
    /// </summary>
    public float Instrumentalness { get; set; }

    /// <summary>
    /// The key the track is in (0-11 mapping to pitch class notation)
    /// 0 = C, 1 = C♯/D♭, 2 = D, ..., 11 = B
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// Detects the presence of an audience in the recording (0.0 to 1.0)
    /// Higher values represent increased probability of live performance
    /// </summary>
    public float Liveness { get; set; }

    /// <summary>
    /// Overall loudness in decibels (dB)
    /// Typically ranges from -60 to 0 dB
    /// </summary>
    public float Loudness { get; set; }

    /// <summary>
    /// Modality (major or minor)
    /// 0 = Minor, 1 = Major
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// Detects the presence of spoken words (0.0 to 1.0)
    /// Values above 0.66 = likely entirely spoken words
    /// 0.33-0.66 = may contain both music and speech
    /// Below 0.33 = most likely music
    /// </summary>
    public float Speechiness { get; set; }

    /// <summary>
    /// Overall estimated tempo in beats per minute (BPM)
    /// Critical for DJ mixing and tempo analysis
    /// </summary>
    public float Tempo { get; set; }

    /// <summary>
    /// Estimated time signature (3-7 representing 3/4 to 7/4 time)
    /// Most common is 4 (4/4 time)
    /// </summary>
    public int TimeSignature { get; set; }

    /// <summary>
    /// Musical positiveness conveyed by a track (0.0 to 1.0)
    /// High valence = positive (happy, cheerful)
    /// Low valence = negative (sad, angry)
    /// </summary>
    public float Valence { get; set; }

    // Navigation property
    public Track Track { get; set; } = null!;
}
