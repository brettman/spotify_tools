using SpotifyTools.Domain.Enums;

namespace SpotifyTools.Sync;

/// <summary>
/// Service for synchronizing Spotify data to local database
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Performs a full sync of all Spotify library data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync history ID</returns>
    Task<int> FullSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an incremental sync of changed data only
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync history ID</returns>
    Task<int> IncrementalSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last successful sync date
    /// </summary>
    Task<DateTime?> GetLastSyncDateAsync();

    /// <summary>
    /// Syncs only tracks (for partial sync)
    /// </summary>
    Task<int> SyncTracksOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs only artists (for partial sync)
    /// </summary>
    Task<int> SyncArtistsOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs only albums (for partial sync)
    /// </summary>
    Task<int> SyncAlbumsOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs only audio features (for partial sync)
    /// </summary>
    Task<int> SyncAudioFeaturesOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs only playlists (for partial sync)
    /// </summary>
    Task<int> SyncPlaylistsOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Progress event for sync operations
    /// </summary>
    event EventHandler<SyncProgressEventArgs>? ProgressChanged;
}

/// <summary>
/// Event args for sync progress updates
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public string Stage { get; set; } = string.Empty;
    public int Current { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = string.Empty;
}
