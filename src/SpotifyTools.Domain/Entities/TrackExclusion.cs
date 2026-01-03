namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Represents a track that has been explicitly excluded from a saved cluster.
/// Allows users to remove specific tracks from genre-based clusters even if
/// the track's artist matches the cluster's genres.
/// </summary>
public class TrackExclusion
{
    /// <summary>
    /// Unique identifier for this exclusion
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The saved cluster from which the track is excluded
    /// </summary>
    public int ClusterId { get; set; }

    /// <summary>
    /// The Spotify track ID being excluded
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// When this track was excluded
    /// </summary>
    public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the saved cluster
    /// </summary>
    public SavedCluster? Cluster { get; set; }

    /// <summary>
    /// Navigation property to the track
    /// </summary>
    public Track? Track { get; set; }
}
