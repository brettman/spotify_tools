using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Analytics;

/// <summary>
/// Report showing the proposed contents of a playlist based on a genre cluster
/// </summary>
public class ClusterPlaylistReport
{
    /// <summary>
    /// The genre cluster this report is for
    /// </summary>
    public GenreCluster Cluster { get; set; } = new();

    /// <summary>
    /// Tracks that would be included in this playlist
    /// </summary>
    public List<TrackInfo> Tracks { get; set; } = new();

    public class TrackInfo
    {
        public string TrackId { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? AlbumName { get; set; }
        public int DurationMs { get; set; }
        public string FormattedDuration { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new();
        public int Popularity { get; set; }
        public DateTime? AddedAt { get; set; }

        /// <summary>
        /// Which genre(s) from the cluster this track matches
        /// </summary>
        public List<string> MatchedGenres { get; set; } = new();
    }
}
