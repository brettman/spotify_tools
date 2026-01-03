using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories;

public interface ITrackExclusionRepository : IRepository<TrackExclusion>
{
    /// <summary>
    /// Get all track exclusions for a specific cluster
    /// </summary>
    Task<List<TrackExclusion>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a track is excluded from a specific cluster
    /// </summary>
    Task<bool> IsTrackExcludedAsync(int clusterId, string trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a track exclusion (exclude a track from a cluster)
    /// </summary>
    Task AddExclusionAsync(int clusterId, string trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a track exclusion (allow a track back into a cluster)
    /// </summary>
    Task RemoveExclusionAsync(int clusterId, string trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all excluded track IDs for a specific cluster
    /// </summary>
    Task<HashSet<string>> GetExcludedTrackIdsAsync(int clusterId, CancellationToken cancellationToken = default);
}
