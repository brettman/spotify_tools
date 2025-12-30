using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for Track entity with specialized queries
/// </summary>
public interface ITrackRepository : IRepository<Track>
{
    /// <summary>
    /// Get tracks with their audio features
    /// </summary>
    Task<IEnumerable<Track>> GetTracksWithAudioFeaturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks by genre (searches artist genres)
    /// </summary>
    Task<IEnumerable<Track>> GetTracksByGenreAsync(string genre, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks added after a specific date
    /// </summary>
    Task<IEnumerable<Track>> GetTracksAddedAfterAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks with full navigation properties (artists, albums, audio features)
    /// </summary>
    Task<Track?> GetTrackWithDetailsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks by tempo range (requires audio features)
    /// </summary>
    Task<IEnumerable<Track>> GetTracksByTempoRangeAsync(float minTempo, float maxTempo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks by musical key (requires audio features)
    /// </summary>
    Task<IEnumerable<Track>> GetTracksByKeyAsync(int key, int? mode = null, CancellationToken cancellationToken = default);
}
