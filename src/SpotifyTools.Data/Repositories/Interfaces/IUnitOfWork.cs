using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Interfaces;

/// <summary>
/// Unit of Work pattern - coordinates multiple repositories and transactions
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository properties
    ITrackRepository Tracks { get; }
    IRepository<Artist> Artists { get; }
    IRepository<Album> Albums { get; }
    IRepository<AudioFeatures> AudioFeatures { get; }
    IRepository<Playlist> Playlists { get; }
    IRepository<PlaylistTrack> PlaylistTracks { get; }
    IRepository<TrackArtist> TrackArtists { get; }
    IRepository<TrackAlbum> TrackAlbums { get; }
    IRepository<SpotifyToken> SpotifyTokens { get; }
    IRepository<SyncHistory> SyncHistory { get; }
    IRepository<AudioAnalysis> AudioAnalyses { get; }
    IRepository<AudioAnalysisSection> AudioAnalysisSections { get; }

    /// <summary>
    /// Save all changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync();
}
