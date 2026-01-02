namespace SpotifyTools.Analytics;

/// <summary>
/// Comprehensive analysis of genre distribution and complexity in the library
/// </summary>
public class GenreAnalysisReport
{
    /// <summary>
    /// Total number of unique genres found
    /// </summary>
    public int TotalGenres { get; set; }

    /// <summary>
    /// Total number of artists in the library
    /// </summary>
    public int TotalArtists { get; set; }

    /// <summary>
    /// Total number of tracks in the library
    /// </summary>
    public int TotalTracks { get; set; }

    /// <summary>
    /// Average number of genres per artist
    /// </summary>
    public double AverageGenresPerArtist { get; set; }

    /// <summary>
    /// Genres ranked by track count (descending)
    /// </summary>
    public List<GenreStats> GenresByTrackCount { get; set; } = new();

    /// <summary>
    /// Distribution of how many genres each artist has
    /// </summary>
    public Dictionary<int, int> GenreCountDistribution { get; set; } = new();

    /// <summary>
    /// Genres that frequently appear together
    /// </summary>
    public List<GenreOverlap> TopGenreOverlaps { get; set; } = new();

    public class GenreStats
    {
        public string GenreName { get; set; } = string.Empty;
        public int ArtistCount { get; set; }
        public int TrackCount { get; set; }
        public double PercentageOfLibrary { get; set; }
    }

    public class GenreOverlap
    {
        public string Genre1 { get; set; } = string.Empty;
        public string Genre2 { get; set; } = string.Empty;
        public int OverlapCount { get; set; }
        public double OverlapPercentage { get; set; }
    }
}
