namespace SpotifyTools.Analytics;

/// <summary>
/// Comprehensive report containing all details about a single track
/// </summary>
public class TrackDetailReport
{
    // Basic Track Info
    public string TrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public bool Explicit { get; set; }
    public int Popularity { get; set; }
    public string? Isrc { get; set; }
    public DateTime? AddedAt { get; set; }

    // Artists
    public List<ArtistInfo> Artists { get; set; } = new();

    // Album
    public AlbumInfo? Album { get; set; }

    // Audio Features
    public AudioFeaturesInfo? AudioFeatures { get; set; }

    // Audio Analysis (section-by-section breakdown)
    public AudioAnalysisInfo? AudioAnalysis { get; set; }

    // Playlists
    public List<string> Playlists { get; set; } = new();

    public class ArtistInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new();
        public int Popularity { get; set; }
        public int Followers { get; set; }
    }

    public class AlbumInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AlbumType { get; set; } = string.Empty;
        public DateTime? ReleaseDate { get; set; }
        public string? Label { get; set; }
        public int TotalTracks { get; set; }
    }

    public class AudioFeaturesInfo
    {
        public float Tempo { get; set; }
        public int Key { get; set; }
        public int Mode { get; set; }
        public int TimeSignature { get; set; }
        public float Danceability { get; set; }
        public float Energy { get; set; }
        public float Acousticness { get; set; }
        public float Instrumentalness { get; set; }
        public float Liveness { get; set; }
        public float Loudness { get; set; }
        public float Speechiness { get; set; }
        public float Valence { get; set; }

        // Helper properties for display
        public string KeyName => GetKeyName(Key);
        public string ModeName => Mode == 1 ? "Major" : "Minor";
        public string TimeSignatureDisplay => $"{TimeSignature}/4";

        private static string GetKeyName(int key)
        {
            return key switch
            {
                0 => "C",
                1 => "C♯/D♭",
                2 => "D",
                3 => "D♯/E♭",
                4 => "E",
                5 => "F",
                6 => "F♯/G♭",
                7 => "G",
                8 => "G♯/A♭",
                9 => "A",
                10 => "A♯/B♭",
                11 => "B",
                _ => "Unknown"
            };
        }
    }

    public class AudioAnalysisInfo
    {
        public float TrackTempo { get; set; }
        public int TrackKey { get; set; }
        public int TrackMode { get; set; }
        public int TrackTimeSignature { get; set; }
        public float Duration { get; set; }
        public List<AudioAnalysisSection> Sections { get; set; } = new();

        // Helper properties for display
        public string KeyName => GetKeyName(TrackKey);
        public string ModeName => TrackMode == 1 ? "Major" : (TrackMode == 0 ? "Minor" : "Unknown");
        public string TimeSignatureDisplay => $"{TrackTimeSignature}/4";

        private static string GetKeyName(int key)
        {
            return key switch
            {
                0 => "C",
                1 => "C♯/D♭",
                2 => "D",
                3 => "D♯/E♭",
                4 => "E",
                5 => "F",
                6 => "F♯/G♭",
                7 => "G",
                8 => "G♯/A♭",
                9 => "A",
                10 => "A♯/B♭",
                11 => "B",
                -1 => "No key detected",
                _ => "Unknown"
            };
        }
    }

    public class AudioAnalysisSection
    {
        public float Start { get; set; }
        public float Duration { get; set; }
        public float Tempo { get; set; }
        public int Key { get; set; }
        public int Mode { get; set; }
        public int TimeSignature { get; set; }
        public float Loudness { get; set; }

        // Helper properties for display
        public string StartTime => TimeSpan.FromSeconds(Start).ToString(@"m\:ss");
        public string KeyName => GetKeyName(Key);
        public string ModeName => Mode == 1 ? "Major" : (Mode == 0 ? "Minor" : "Unknown");
        public string TimeSignatureDisplay => $"{TimeSignature}/4";

        private static string GetKeyName(int key)
        {
            return key switch
            {
                0 => "C",
                1 => "C♯/D♭",
                2 => "D",
                3 => "D♯/E♭",
                4 => "E",
                5 => "F",
                6 => "F♯/G♭",
                7 => "G",
                8 => "G♯/A♭",
                9 => "A",
                10 => "A♯/B♭",
                11 => "B",
                -1 => "No key detected",
                _ => "Unknown"
            };
        }
    }

    // Helper method to format duration
    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(DurationMs);
            return ts.TotalHours >= 1
                ? $"{ts.Hours:D1}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D1}:{ts.Seconds:D2}";
        }
    }
}
