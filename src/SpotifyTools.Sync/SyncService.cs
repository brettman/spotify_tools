using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyClientService;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Domain.Enums;
using System.Net;

namespace SpotifyTools.Sync;

/// <summary>
/// Service for synchronizing Spotify data to local database
/// </summary>
public class SyncService : ISyncService
{
    private readonly ISpotifyClientService _spotifyClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SyncService> _logger;
    private readonly RateLimiter _rateLimiter;

    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

    public SyncService(
        ISpotifyClientService spotifyClient,
        IUnitOfWork unitOfWork,
        ILogger<SyncService> logger)
    {
        _spotifyClient = spotifyClient ?? throw new ArgumentNullException(nameof(spotifyClient));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiter = new RateLimiter(30, TimeSpan.FromMinutes(1)); // 30 requests per minute (conservative)
    }

    public async Task<int> FullSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full sync of Spotify library");

        // Create sync history record
        var syncHistory = new SyncHistory
        {
            SyncType = SyncType.Full,
            Status = SyncStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        await _unitOfWork.SyncHistory.AddAsync(syncHistory);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            // Ensure authenticated
            if (!_spotifyClient.IsAuthenticated)
            {
                await _spotifyClient.AuthenticateAsync();
            }

            // Sync in order: Tracks -> Artists -> Albums -> Audio Features -> Playlists
            var stats = new SyncStats();

            OnProgressChanged("Tracks", 0, 0, "Fetching saved tracks...");
            stats.TracksProcessed = await SyncTracksAsync(cancellationToken);

            OnProgressChanged("Artists", 0, 0, "Fetching artist details...");
            stats.ArtistsProcessed = await SyncArtistsAsync(cancellationToken);

            OnProgressChanged("Albums", 0, 0, "Fetching album details...");
            stats.AlbumsProcessed = await SyncAlbumsAsync(cancellationToken);

            OnProgressChanged("Audio Features", 0, 0, "Fetching audio features...");
            stats.AudioFeaturesProcessed = await SyncAudioFeaturesAsync(cancellationToken);

            OnProgressChanged("Playlists", 0, 0, "Fetching playlists...");
            stats.PlaylistsProcessed = await SyncPlaylistsAsync(cancellationToken);

            // Update sync history
            syncHistory.Status = SyncStatus.Success;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.TracksAdded = stats.TracksProcessed;
            syncHistory.ArtistsAdded = stats.ArtistsProcessed;
            syncHistory.AlbumsAdded = stats.AlbumsProcessed;
            syncHistory.PlaylistsSynced = stats.PlaylistsProcessed;
            syncHistory.ErrorMessage = null;

            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Full sync completed successfully. Tracks: {Tracks}, Artists: {Artists}, Albums: {Albums}, " +
                "Audio Features: {AudioFeatures}, Playlists: {Playlists}",
                stats.TracksProcessed, stats.ArtistsProcessed, stats.AlbumsProcessed,
                stats.AudioFeaturesProcessed, stats.PlaylistsProcessed);

            OnProgressChanged("Complete", 1, 1, "Sync completed successfully!");

            return syncHistory.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed");

            syncHistory.Status = SyncStatus.Failed;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.ErrorMessage = ex.Message;

            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            throw;
        }
    }

    public async Task<int> IncrementalSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Incremental sync not yet implemented");
        throw new NotImplementedException("Incremental sync will be implemented in Phase 2");
    }

    public async Task<DateTime?> GetLastSyncDateAsync()
    {
        var allSyncs = await _unitOfWork.SyncHistory.GetAllAsync();

        return allSyncs
            .Where(s => s.Status == SyncStatus.Success)
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefault()
            ?.CompletedAt;
    }

    /// <summary>
    /// Executes an API call with automatic retry logic for rate limiting (429 errors)
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> apiCall,
        string operationName,
        int maxRetries = 3)
    {
        var retryCount = 0;

        while (true)
        {
            try
            {
                var result = await apiCall();

                // Success! Reset the global backoff counter
                _rateLimiter.ResetBackoff();

                return result;
            }
            catch (APITooManyRequestsException ex)
            {
                retryCount++;

                // Log the Retry-After header to see what Spotify is telling us
                var retryAfterHeader = ex.Response?.Headers?.ContainsKey("Retry-After") == true
                    ? ex.Response.Headers["Retry-After"]
                    : "not provided";

                _logger.LogWarning(
                    "Rate limit hit for {Operation}. Retry-After: {RetryAfter}, Attempt {Retry}/{Max}",
                    operationName, retryAfterHeader, retryCount, maxRetries);

                if (retryCount > maxRetries)
                {
                    _logger.LogError(
                        "Max retries ({MaxRetries}) exceeded for {Operation}. " +
                        "This may indicate a daily/hourly quota limit has been reached. " +
                        "Try again later or reduce sync frequency.",
                        maxRetries, operationName);
                    throw;
                }

                // Trigger global backoff to pause ALL API calls
                _rateLimiter.TriggerBackoff();

                _logger.LogWarning("Waiting for global backoff before retry {Retry}/{Max}...", retryCount, maxRetries);

                // CRITICAL FIX: Must call WaitAsync() before retry so it respects the global backoff
                await _rateLimiter.WaitAsync();
            }
            catch (Exception)
            {
                // Re-throw other exceptions immediately
                throw;
            }
        }
    }

    private async Task<int> SyncTracksAsync(CancellationToken cancellationToken)
    {
        var tracksProcessed = 0;
        var offset = 0;
        const int limit = 50; // Spotify API max per request
        var now = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var savedTracks = await _spotifyClient.Client.Library.GetTracks(
                new LibraryTracksRequest { Limit = limit, Offset = offset });

            if (savedTracks.Items == null || savedTracks.Items.Count == 0)
                break;

            foreach (var savedTrack in savedTracks.Items)
            {
                if (savedTrack.Track == null) continue;

                var track = new Track
                {
                    Id = savedTrack.Track.Id,
                    Name = savedTrack.Track.Name,
                    DurationMs = savedTrack.Track.DurationMs,
                    Explicit = savedTrack.Track.Explicit,
                    Popularity = savedTrack.Track.Popularity,
                    Isrc = savedTrack.Track.ExternalIds?.ContainsKey("isrc") == true
                        ? savedTrack.Track.ExternalIds["isrc"]
                        : null,
                    AddedAt = DateTime.SpecifyKind(savedTrack.AddedAt, DateTimeKind.Utc),
                    FirstSyncedAt = now,
                    LastSyncedAt = now
                };

                // Check if track already exists
                var existingTrack = await _unitOfWork.Tracks.GetByIdAsync(track.Id);
                if (existingTrack == null)
                {
                    await _unitOfWork.Tracks.AddAsync(track);
                }
                else
                {
                    existingTrack.Name = track.Name;
                    existingTrack.DurationMs = track.DurationMs;
                    existingTrack.Explicit = track.Explicit;
                    existingTrack.Popularity = track.Popularity;
                    existingTrack.Isrc = track.Isrc;
                    existingTrack.LastSyncedAt = now;
                    _unitOfWork.Tracks.Update(existingTrack);
                }

                // Create stub artist records if they don't exist (will be updated with full details later)
                var artistPosition = 0;
                foreach (var artist in savedTrack.Track.Artists)
                {
                    var existingArtist = await _unitOfWork.Artists.GetByIdAsync(artist.Id);
                    if (existingArtist == null)
                    {
                        // Create minimal artist record
                        var stubArtist = new Artist
                        {
                            Id = artist.Id,
                            Name = artist.Name,
                            Genres = Array.Empty<string>(),
                            Popularity = 0,
                            Followers = 0,
                            FirstSyncedAt = now,
                            LastSyncedAt = now
                        };
                        await _unitOfWork.Artists.AddAsync(stubArtist);
                    }

                    // Create track-artist relationship
                    var trackArtist = new TrackArtist
                    {
                        TrackId = track.Id,
                        ArtistId = artist.Id,
                        Position = artistPosition++
                    };

                    var existingRelation = (await _unitOfWork.TrackArtists.GetAllAsync())
                        .FirstOrDefault(ta => ta.TrackId == track.Id && ta.ArtistId == artist.Id);

                    if (existingRelation == null)
                    {
                        await _unitOfWork.TrackArtists.AddAsync(trackArtist);
                    }
                }

                // Create stub album record if it doesn't exist (will be updated with full details later)
                if (savedTrack.Track.Album != null)
                {
                    var existingAlbum = await _unitOfWork.Albums.GetByIdAsync(savedTrack.Track.Album.Id);
                    if (existingAlbum == null)
                    {
                        // Create minimal album record
                        var stubAlbum = new Album
                        {
                            Id = savedTrack.Track.Album.Id,
                            Name = savedTrack.Track.Album.Name,
                            AlbumType = savedTrack.Track.Album.AlbumType ?? "album",
                            ReleaseDate = ParseReleaseDate(savedTrack.Track.Album.ReleaseDate),
                            TotalTracks = savedTrack.Track.Album.TotalTracks,
                            Label = null,
                            ImageUrl = null,
                            FirstSyncedAt = now,
                            LastSyncedAt = now
                        };
                        await _unitOfWork.Albums.AddAsync(stubAlbum);
                    }

                    // Create track-album relationship
                    var trackAlbum = new TrackAlbum
                    {
                        TrackId = track.Id,
                        AlbumId = savedTrack.Track.Album.Id,
                        DiscNumber = savedTrack.Track.DiscNumber,
                        TrackNumber = savedTrack.Track.TrackNumber
                    };

                    var existingAlbumRelation = (await _unitOfWork.TrackAlbums.GetAllAsync())
                        .FirstOrDefault(ta => ta.TrackId == track.Id && ta.AlbumId == savedTrack.Track.Album.Id);

                    if (existingAlbumRelation == null)
                    {
                        await _unitOfWork.TrackAlbums.AddAsync(trackAlbum);
                    }
                }

                tracksProcessed++;
            }

            await _unitOfWork.SaveChangesAsync();

            OnProgressChanged("Tracks", tracksProcessed, savedTracks.Total ?? tracksProcessed,
                $"Processed {tracksProcessed} of {savedTracks.Total ?? tracksProcessed} tracks");

            offset += limit;

            if (offset >= savedTracks.Total)
                break;
        }

        _logger.LogInformation("Synced {Count} tracks", tracksProcessed);
        return tracksProcessed;
    }

    private async Task<int> SyncArtistsAsync(CancellationToken cancellationToken)
    {
        var artistsProcessed = 0;
        var now = DateTime.UtcNow;

        // Get all unique artist IDs from track_artists table
        var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
        var allArtistIds = trackArtists.Select(ta => ta.ArtistId).Distinct().ToList();

        // Get artist IDs that already have full details (not stubs)
        var completedArtistIds = (await _unitOfWork.Artists.GetAllAsync())
            .Where(a => a.Genres.Length > 0)
            .Select(a => a.Id)
            .ToHashSet();

        // Only sync artists that don't exist or are stubs
        var artistIdsToSync = allArtistIds.Where(id => !completedArtistIds.Contains(id)).ToList();

        var totalArtists = allArtistIds.Count;
        var skippedCount = totalArtists - artistIdsToSync.Count;

        _logger.LogInformation("Syncing {ToSync} artists ({Skipped} already completed)",
            artistIdsToSync.Count, skippedCount);

        // Create a dictionary of existing artists for update checks
        var existingArtistsDict = (await _unitOfWork.Artists.GetAllAsync())
            .Where(a => artistIdsToSync.Contains(a.Id))
            .ToDictionary(a => a.Id);

        foreach (var artistId in artistIdsToSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            try
            {
                var spotifyArtist = await ExecuteWithRetryAsync(
                    () => _spotifyClient.Client.Artists.Get(artistId),
                    $"Artist {artistId}");

                existingArtistsDict.TryGetValue(artistId, out var existingArtist);

                var artist = new Artist
                {
                    Id = spotifyArtist.Id,
                    Name = spotifyArtist.Name,
                    Genres = spotifyArtist.Genres?.ToArray() ?? Array.Empty<string>(),
                    Popularity = spotifyArtist.Popularity,
                    Followers = spotifyArtist.Followers?.Total ?? 0,
                    FirstSyncedAt = existingArtist?.FirstSyncedAt ?? now,
                    LastSyncedAt = now
                };

                if (existingArtist == null)
                {
                    await _unitOfWork.Artists.AddAsync(artist);
                }
                else
                {
                    existingArtist.Name = artist.Name;
                    existingArtist.Genres = artist.Genres;
                    existingArtist.Popularity = artist.Popularity;
                    existingArtist.Followers = artist.Followers;
                    existingArtist.LastSyncedAt = now;
                    _unitOfWork.Artists.Update(existingArtist);
                }

                artistsProcessed++;

                if (artistsProcessed % 10 == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    OnProgressChanged("Artists", artistsProcessed, artistIdsToSync.Count,
                        $"Synced {artistsProcessed} of {artistIdsToSync.Count} artists ({skippedCount} already completed)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch artist {ArtistId}", artistId);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        OnProgressChanged("Artists", artistsProcessed, artistIdsToSync.Count, $"Completed {artistsProcessed} artists");

        _logger.LogInformation("Synced {Count} artists", artistsProcessed);
        return artistsProcessed;
    }

    private async Task<int> SyncAlbumsAsync(CancellationToken cancellationToken)
    {
        var albumsProcessed = 0;
        var now = DateTime.UtcNow;

        // Get all unique album IDs from track_albums table
        var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
        var allAlbumIds = trackAlbums.Select(ta => ta.AlbumId).Distinct().ToList();

        // Get album IDs that already have full details (not stubs)
        var completedAlbumIds = (await _unitOfWork.Albums.GetAllAsync())
            .Where(a => !string.IsNullOrEmpty(a.Label))
            .Select(a => a.Id)
            .ToHashSet();

        // Only sync albums that don't exist or are stubs
        var albumIdsToSync = allAlbumIds.Where(id => !completedAlbumIds.Contains(id)).ToList();

        var totalAlbums = allAlbumIds.Count;
        var skippedCount = totalAlbums - albumIdsToSync.Count;

        _logger.LogInformation("Syncing {ToSync} albums ({Skipped} already completed)",
            albumIdsToSync.Count, skippedCount);

        // Create a dictionary of existing albums for update checks
        var existingAlbumsDict = (await _unitOfWork.Albums.GetAllAsync())
            .Where(a => albumIdsToSync.Contains(a.Id))
            .ToDictionary(a => a.Id);

        foreach (var albumId in albumIdsToSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            try
            {
                var spotifyAlbum = await ExecuteWithRetryAsync(
                    () => _spotifyClient.Client.Albums.Get(albumId),
                    $"Album {albumId}");

                existingAlbumsDict.TryGetValue(albumId, out var existingAlbum);

                var album = new Album
                {
                    Id = spotifyAlbum.Id,
                    Name = spotifyAlbum.Name,
                    ReleaseDate = ParseReleaseDate(spotifyAlbum.ReleaseDate),
                    TotalTracks = spotifyAlbum.TotalTracks,
                    AlbumType = spotifyAlbum.AlbumType,
                    Label = spotifyAlbum.Label,
                    FirstSyncedAt = existingAlbum?.FirstSyncedAt ?? now,
                    LastSyncedAt = now
                };

                if (existingAlbum == null)
                {
                    await _unitOfWork.Albums.AddAsync(album);
                }
                else
                {
                    existingAlbum.Name = album.Name;
                    existingAlbum.ReleaseDate = album.ReleaseDate;
                    existingAlbum.TotalTracks = album.TotalTracks;
                    existingAlbum.AlbumType = album.AlbumType;
                    existingAlbum.Label = album.Label;
                    existingAlbum.LastSyncedAt = now;
                    _unitOfWork.Albums.Update(existingAlbum);
                }

                albumsProcessed++;

                if (albumsProcessed % 10 == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    OnProgressChanged("Albums", albumsProcessed, albumIdsToSync.Count,
                        $"Synced {albumsProcessed} of {albumIdsToSync.Count} albums ({skippedCount} already completed)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch album {AlbumId}", albumId);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        OnProgressChanged("Albums", albumsProcessed, albumIdsToSync.Count, $"Completed {albumsProcessed} albums");

        _logger.LogInformation("Synced {Count} albums", albumsProcessed);
        return albumsProcessed;
    }

    private async Task<int> SyncAudioFeaturesAsync(CancellationToken cancellationToken)
    {
        var audioFeaturesProcessed = 0;

        // Get all track IDs that don't have audio features yet
        var tracks = await _unitOfWork.Tracks.GetAllAsync();
        var existingAudioFeatures = await _unitOfWork.AudioFeatures.GetAllAsync();
        var existingTrackIds = existingAudioFeatures.Select(af => af.TrackId).ToHashSet();

        var trackIds = tracks
            .Where(t => !existingTrackIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();

        var total = trackIds.Count;

        // Process in batches of 100 (Spotify API limit)
        const int batchSize = 100;
        for (var i = 0; i < trackIds.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var batch = trackIds.Skip(i).Take(batchSize).ToList();

            try
            {
                var request = new TracksAudioFeaturesRequest(batch);
                var audioFeaturesResponse = await _spotifyClient.Client.Tracks.GetSeveralAudioFeatures(request);

                foreach (var af in audioFeaturesResponse.AudioFeatures)
                {
                    if (af == null) continue;

                    var audioFeatures = new AudioFeatures
                    {
                        TrackId = af.Id,
                        Acousticness = af.Acousticness,
                        Danceability = af.Danceability,
                        Energy = af.Energy,
                        Instrumentalness = af.Instrumentalness,
                        Key = af.Key,
                        Liveness = af.Liveness,
                        Loudness = af.Loudness,
                        Mode = af.Mode,
                        Speechiness = af.Speechiness,
                        Tempo = af.Tempo,
                        TimeSignature = af.TimeSignature,
                        Valence = af.Valence
                    };

                    await _unitOfWork.AudioFeatures.AddAsync(audioFeatures);
                    audioFeaturesProcessed++;
                }

                await _unitOfWork.SaveChangesAsync();
                OnProgressChanged("Audio Features", audioFeaturesProcessed, total,
                    $"Processed {audioFeaturesProcessed} of {total} audio features");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch audio features for batch starting at {Index}", i);
            }
        }

        _logger.LogInformation("Synced {Count} audio features", audioFeaturesProcessed);
        return audioFeaturesProcessed;
    }

    private async Task<int> SyncPlaylistsAsync(CancellationToken cancellationToken)
    {
        var playlistsProcessed = 0;
        var offset = 0;
        const int limit = 50;
        var now = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var playlistsPage = await _spotifyClient.Client.Playlists.GetUsers(_spotifyClient.UserId!,
                new PlaylistGetUsersRequest { Limit = limit, Offset = offset });

            if (playlistsPage.Items == null || playlistsPage.Items.Count == 0)
                break;

            foreach (var spotifyPlaylist in playlistsPage.Items)
            {
                var playlist = new Playlist
                {
                    Id = spotifyPlaylist.Id!,
                    Name = spotifyPlaylist.Name!,
                    Description = spotifyPlaylist.Description,
                    OwnerId = spotifyPlaylist.Owner?.Id ?? string.Empty,
                    IsPublic = spotifyPlaylist.Public ?? false,
                    SnapshotId = spotifyPlaylist.SnapshotId!,
                    FirstSyncedAt = now,
                    LastSyncedAt = now
                };

                var existingPlaylist = await _unitOfWork.Playlists.GetByIdAsync(playlist.Id);
                if (existingPlaylist == null)
                {
                    await _unitOfWork.Playlists.AddAsync(playlist);
                }
                else
                {
                    existingPlaylist.Name = playlist.Name;
                    existingPlaylist.Description = playlist.Description;
                    existingPlaylist.OwnerId = playlist.OwnerId;
                    existingPlaylist.IsPublic = playlist.IsPublic;
                    existingPlaylist.SnapshotId = playlist.SnapshotId;
                    existingPlaylist.LastSyncedAt = now;
                    _unitOfWork.Playlists.Update(existingPlaylist);
                }

                // Sync playlist tracks
                await SyncPlaylistTracksAsync(playlist.Id, cancellationToken);

                playlistsProcessed++;
            }

            await _unitOfWork.SaveChangesAsync();
            OnProgressChanged("Playlists", playlistsProcessed, playlistsPage.Total ?? playlistsProcessed,
                $"Processed {playlistsProcessed} of {playlistsPage.Total ?? playlistsProcessed} playlists");

            offset += limit;

            if (offset >= playlistsPage.Total)
                break;
        }

        _logger.LogInformation("Synced {Count} playlists", playlistsProcessed);
        return playlistsProcessed;
    }

    private async Task SyncPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken)
    {
        // Remove existing playlist tracks
        var existingPlaylistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
            .Where(pt => pt.PlaylistId == playlistId)
            .ToList();

        foreach (var track in existingPlaylistTracks)
        {
            _unitOfWork.PlaylistTracks.Delete(track);
        }

        var offset = 0;
        const int limit = 100;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var playlistTracks = await _spotifyClient.Client.Playlists.GetItems(playlistId,
                new PlaylistGetItemsRequest { Limit = limit, Offset = offset });

            if (playlistTracks.Items == null || playlistTracks.Items.Count == 0)
                break;

            foreach (var item in playlistTracks.Items)
            {
                if (item.Track is FullTrack fullTrack)
                {
                    var playlistTrack = new PlaylistTrack
                    {
                        PlaylistId = playlistId,
                        TrackId = fullTrack.Id,
                        Position = offset + playlistTracks.Items.IndexOf(item),
                        AddedAt = item.AddedAt.HasValue
                            ? DateTime.SpecifyKind(item.AddedAt.Value, DateTimeKind.Utc)
                            : DateTime.UtcNow
                    };

                    await _unitOfWork.PlaylistTracks.AddAsync(playlistTrack);
                }
            }

            offset += limit;

            if (offset >= playlistTracks.Total)
                break;
        }
    }

    private void OnProgressChanged(string stage, int current, int total, string message)
    {
        ProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            Stage = stage,
            Current = current,
            Total = total,
            Message = message
        });

        _logger.LogInformation("[{Stage}] {Message}", stage, message);
    }

    private static DateTime? ParseReleaseDate(string? releaseDateString)
    {
        if (string.IsNullOrWhiteSpace(releaseDateString))
            return null;

        // Spotify release dates can be YYYY, YYYY-MM, or YYYY-MM-DD
        if (DateTime.TryParse(releaseDateString, out var date))
        {
            // Specify as UTC since PostgreSQL requires it
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return null;
    }

    private class SyncStats
    {
        public int TracksProcessed { get; set; }
        public int ArtistsProcessed { get; set; }
        public int AlbumsProcessed { get; set; }
        public int AudioFeaturesProcessed { get; set; }
        public int PlaylistsProcessed { get; set; }
    }
}
