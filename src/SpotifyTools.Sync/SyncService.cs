using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyClientService;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Domain.Enums;
using SpotifyTools.Sync.Models;
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
    private readonly HashSet<string> _missingPlaylistTrackIds = new();

    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

    // Incremental sync configuration constants
    private const int METADATA_REFRESH_DAYS = 7;
    private const int FULL_SYNC_FALLBACK_DAYS = 30;

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

            // Sync in order: Tracks -> Artists -> Albums -> Playlists
            // Audio Features disabled (batch API deprecated, individual calls too slow)
            var stats = new SyncStats();

            OnProgressChanged("Tracks", 0, 0, "Fetching saved tracks...");
            stats.TracksProcessed = await SyncTracksAsync(cancellationToken);

            OnProgressChanged("Artists", 0, 0, "Fetching artist details...");
            stats.ArtistsProcessed = await SyncArtistsAsync(cancellationToken);

            OnProgressChanged("Albums", 0, 0, "Fetching album details...");
            stats.AlbumsProcessed = await SyncAlbumsAsync(cancellationToken);

            // Audio Features sync - TESTING WITH LIMITED BATCH
            OnProgressChanged("Audio Features", 0, 0, "Fetching audio features (TEST: limited to 10 tracks)...");
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

            // Store missing playlist track IDs as JSON
            if (_missingPlaylistTrackIds.Count > 0)
            {
                syncHistory.MissingPlaylistTrackIds = System.Text.Json.JsonSerializer.Serialize(_missingPlaylistTrackIds);
                _logger.LogInformation(
                    "Found {Count} tracks in playlists that are not in saved library. " +
                    "Run an incremental sync to fetch these tracks.",
                    _missingPlaylistTrackIds.Count);
            }

            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Full sync completed successfully. Tracks: {Tracks}, Artists: {Artists}, Albums: {Albums}, Playlists: {Playlists}",
                stats.TracksProcessed, stats.ArtistsProcessed, stats.AlbumsProcessed, stats.PlaylistsProcessed);

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
        _logger.LogInformation("Starting incremental sync of Spotify library");

        // Get last successful sync
        var lastSync = await GetLastSyncDateAsync();

        // Check if should fallback to full sync
        if (ShouldUseFullSync(lastSync))
        {
            _logger.LogInformation("Falling back to full sync");
            return await FullSyncAsync(cancellationToken);
        }

        // Create sync history record
        var syncHistory = new SyncHistory
        {
            SyncType = SyncType.Incremental,
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

            var metadataThreshold = DateTime.UtcNow.AddDays(-METADATA_REFRESH_DAYS);
            _logger.LogInformation("Metadata refresh threshold: {Threshold} ({Days} days ago)",
                metadataThreshold, METADATA_REFRESH_DAYS);

            // Sync new tracks
            OnProgressChanged("Tracks", 0, 0, "Checking for new tracks...");
            var tracksAdded = await IncrementalSyncTracksAsync(lastSync!.Value, cancellationToken);

            // Sync artists (stubs + stale)
            OnProgressChanged("Artists", 0, 0, "Syncing artist metadata...");
            var (artistsAdded, artistsUpdated) = await IncrementalSyncArtistsAsync(metadataThreshold, cancellationToken);

            // Sync albums (stubs + stale)
            OnProgressChanged("Albums", 0, 0, "Syncing album metadata...");
            var (albumsAdded, albumsUpdated) = await IncrementalSyncAlbumsAsync(metadataThreshold, cancellationToken);

            // Sync audio features (new tracks only)
            OnProgressChanged("Audio Features", 0, 0, "Processing audio features...");
            var audioFeaturesProcessed = await SyncAudioFeaturesAsync(cancellationToken);

            // Sync playlists (using SnapshotId)
            OnProgressChanged("Playlists", 0, 0, "Checking playlists for changes...");
            var (playlistsTotal, playlistsChanged) = await IncrementalSyncPlaylistsAsync(cancellationToken);

            // Update sync history
            syncHistory.Status = SyncStatus.Success;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.TracksAdded = tracksAdded;
            syncHistory.TracksUpdated = 0; // Incremental sync doesn't track track updates separately
            syncHistory.ArtistsAdded = artistsAdded;
            syncHistory.AlbumsAdded = albumsAdded;
            syncHistory.PlaylistsSynced = playlistsChanged; // Only count changed playlists
            syncHistory.ErrorMessage = null;

            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Incremental sync completed successfully. New Tracks: {Tracks}, Artists: {Artists} ({ArtistsUpdated} updated), " +
                "Albums: {Albums} ({AlbumsUpdated} updated), Audio Features: {AudioFeatures}, " +
                "Playlists: {PlaylistsChanged}/{PlaylistsTotal} changed",
                tracksAdded, artistsAdded, artistsUpdated, albumsAdded, albumsUpdated,
                audioFeaturesProcessed, playlistsChanged, playlistsTotal);

            OnProgressChanged("Complete", 1, 1, "Incremental sync completed successfully!");

            return syncHistory.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Incremental sync failed");

            syncHistory.Status = SyncStatus.Failed;
            syncHistory.CompletedAt = DateTime.UtcNow;
            syncHistory.ErrorMessage = ex.Message;

            _unitOfWork.SyncHistory.Update(syncHistory);
            await _unitOfWork.SaveChangesAsync();

            throw;
        }
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
    /// Determines if a full sync should be used instead of incremental
    /// </summary>
    /// <param name="lastSync">The date of the last successful sync</param>
    /// <returns>True if full sync should be used, false if incremental sync is appropriate</returns>
    private bool ShouldUseFullSync(DateTime? lastSync)
    {
        // No previous sync exists - must do full sync
        if (lastSync == null)
            return true;

        // Last sync was more than 30 days ago - fallback to full sync
        var daysSinceLastSync = (DateTime.UtcNow - lastSync.Value).TotalDays;
        if (daysSinceLastSync > FULL_SYNC_FALLBACK_DAYS)
        {
            _logger.LogInformation(
                "Last sync was {Days:F1} days ago (> {Threshold} days). Falling back to full sync.",
                daysSinceLastSync, FULL_SYNC_FALLBACK_DAYS);
            return true;
        }

        // Recent sync exists - use incremental
        return false;
    }

    public async Task<int> SyncTracksOnlyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting tracks-only sync");
        if (!_spotifyClient.IsAuthenticated)
            await _spotifyClient.AuthenticateAsync();

        return await SyncTracksAsync(cancellationToken);
    }

    public async Task<int> SyncArtistsOnlyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting artists-only sync");
        if (!_spotifyClient.IsAuthenticated)
            await _spotifyClient.AuthenticateAsync();

        return await SyncArtistsAsync(cancellationToken);
    }

    public async Task<int> SyncAlbumsOnlyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting albums-only sync");
        if (!_spotifyClient.IsAuthenticated)
            await _spotifyClient.AuthenticateAsync();

        return await SyncAlbumsAsync(cancellationToken);
    }

    public async Task<int> SyncAudioFeaturesOnlyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting audio features-only sync");
        if (!_spotifyClient.IsAuthenticated)
            await _spotifyClient.AuthenticateAsync();

        return await SyncAudioFeaturesAsync(cancellationToken);
    }

    public async Task<int> SyncPlaylistsOnlyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting playlists-only sync");
        if (!_spotifyClient.IsAuthenticated)
            await _spotifyClient.AuthenticateAsync();

        return await SyncPlaylistsAsync(cancellationToken);
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

    /// <summary>
    /// Incrementally syncs only tracks added since the last sync date
    /// </summary>
    private async Task<int> IncrementalSyncTracksAsync(DateTime lastSyncDate, CancellationToken cancellationToken)
    {
        var newTracksAdded = 0;
        var offset = 0;
        const int limit = 50; // Spotify API max per request
        var now = DateTime.UtcNow;

        _logger.LogInformation("Checking for tracks added after {LastSync}", lastSyncDate);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var savedTracks = await ExecuteWithRetryAsync(
                () => _spotifyClient.Client.Library.GetTracks(
                    new LibraryTracksRequest { Limit = limit, Offset = offset }),
                $"Library tracks (offset {offset})");

            if (savedTracks.Items == null || savedTracks.Items.Count == 0)
                break;

            foreach (var savedTrack in savedTracks.Items)
            {
                if (savedTrack.Track == null) continue;

                // Filter: only process tracks added after last sync
                var addedAt = DateTime.SpecifyKind(savedTrack.AddedAt, DateTimeKind.Utc);
                if (addedAt <= lastSyncDate)
                    continue; // Skip tracks added before last sync

                // Track is new - upsert it
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
                    AddedAt = addedAt,
                    FirstSyncedAt = now,
                    LastSyncedAt = now
                };

                // Check if track already exists (shouldn't for new tracks, but handle it)
                var existingTrack = await _unitOfWork.Tracks.GetByIdAsync(track.Id);
                if (existingTrack == null)
                {
                    await _unitOfWork.Tracks.AddAsync(track);
                }
                else
                {
                    // Update existing track metadata
                    existingTrack.Name = track.Name;
                    existingTrack.DurationMs = track.DurationMs;
                    existingTrack.Explicit = track.Explicit;
                    existingTrack.Popularity = track.Popularity;
                    existingTrack.Isrc = track.Isrc;
                    existingTrack.LastSyncedAt = now;
                    _unitOfWork.Tracks.Update(existingTrack);
                }

                // Create stub artist records if they don't exist
                var artistPosition = 0;
                foreach (var artist in savedTrack.Track.Artists)
                {
                    var existingArtist = await _unitOfWork.Artists.GetByIdAsync(artist.Id);
                    if (existingArtist == null)
                    {
                        // Create minimal artist record (will be enriched later)
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

                // Create stub album record if it doesn't exist
                if (savedTrack.Track.Album != null)
                {
                    var existingAlbum = await _unitOfWork.Albums.GetByIdAsync(savedTrack.Track.Album.Id);
                    if (existingAlbum == null)
                    {
                        // Create minimal album record (will be enriched later)
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

                newTracksAdded++;
            }

            await _unitOfWork.SaveChangesAsync();

            OnProgressChanged("Tracks", offset + savedTracks.Items.Count, savedTracks.Total ?? offset + savedTracks.Items.Count,
                $"Checked {offset + savedTracks.Items.Count} tracks, found {newTracksAdded} new");

            offset += limit;

            if (offset >= savedTracks.Total)
                break;
        }

        _logger.LogInformation("Incremental sync found {Count} new tracks", newTracksAdded);
        return newTracksAdded;
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

    /// <summary>
    /// Incrementally syncs artists that are stubs or have stale metadata
    /// </summary>
    /// <param name="metadataThreshold">Sync artists with LastSyncedAt before this date</param>
    /// <returns>Tuple of (artists added, artists updated)</returns>
    private async Task<(int Added, int Updated)> IncrementalSyncArtistsAsync(DateTime metadataThreshold, CancellationToken cancellationToken)
    {
        var artistsAdded = 0;
        var artistsUpdated = 0;
        var now = DateTime.UtcNow;

        _logger.LogInformation("Syncing artists: stubs + stale metadata (before {Threshold})", metadataThreshold);

        // Get all unique artist IDs from track_artists table
        var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
        var allArtistIds = trackArtists.Select(ta => ta.ArtistId).Distinct().ToList();

        // Get all artists from database
        var allArtists = await _unitOfWork.Artists.GetAllAsync();
        var artistsDict = allArtists.ToDictionary(a => a.Id);

        // Identify artists to sync:
        // 1. Stubs: Genres.Length == 0
        // 2. Stale: LastSyncedAt < metadataThreshold
        var artistIdsToSync = allArtistIds.Where(id =>
        {
            if (!artistsDict.TryGetValue(id, out var artist))
                return false; // Artist doesn't exist (shouldn't happen, but skip)

            // Sync if it's a stub (no genres)
            if (artist.Genres.Length == 0)
                return true;

            // Sync if metadata is stale
            if (artist.LastSyncedAt < metadataThreshold)
                return true;

            return false;
        }).ToList();

        var totalArtists = allArtistIds.Count;
        var skippedCount = totalArtists - artistIdsToSync.Count;

        _logger.LogInformation("Syncing {ToSync} artists ({Skipped} up-to-date)",
            artistIdsToSync.Count, skippedCount);

        foreach (var artistId in artistIdsToSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            try
            {
                var spotifyArtist = await ExecuteWithRetryAsync(
                    () => _spotifyClient.Client.Artists.Get(artistId),
                    $"Artist {artistId}");

                artistsDict.TryGetValue(artistId, out var existingArtist);

                // Track if this was a stub (for counting)
                var wasStub = existingArtist?.Genres.Length == 0;

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
                    artistsAdded++;
                }
                else
                {
                    existingArtist.Name = artist.Name;
                    existingArtist.Genres = artist.Genres;
                    existingArtist.Popularity = artist.Popularity;
                    existingArtist.Followers = artist.Followers;
                    existingArtist.LastSyncedAt = now;
                    _unitOfWork.Artists.Update(existingArtist);

                    if (wasStub)
                        artistsAdded++; // Count stub enrichment as "added"
                    else
                        artistsUpdated++; // Count refresh as "updated"
                }

                if ((artistsAdded + artistsUpdated) % 10 == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    OnProgressChanged("Artists", artistsAdded + artistsUpdated, artistIdsToSync.Count,
                        $"Synced {artistsAdded + artistsUpdated} of {artistIdsToSync.Count} artists ({skippedCount} up-to-date)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch artist {ArtistId}", artistId);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        OnProgressChanged("Artists", artistsAdded + artistsUpdated, artistIdsToSync.Count,
            $"Completed: {artistsAdded} added, {artistsUpdated} updated");

        _logger.LogInformation("Incremental artist sync: {Added} added, {Updated} updated", artistsAdded, artistsUpdated);
        return (artistsAdded, artistsUpdated);
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

    /// <summary>
    /// Incrementally syncs albums that are stubs or have stale metadata
    /// </summary>
    /// <param name="metadataThreshold">Sync albums with LastSyncedAt before this date</param>
    /// <returns>Tuple of (albums added, albums updated)</returns>
    private async Task<(int Added, int Updated)> IncrementalSyncAlbumsAsync(DateTime metadataThreshold, CancellationToken cancellationToken)
    {
        var albumsAdded = 0;
        var albumsUpdated = 0;
        var now = DateTime.UtcNow;

        _logger.LogInformation("Syncing albums: stubs + stale metadata (before {Threshold})", metadataThreshold);

        // Get all unique album IDs from track_albums table
        var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
        var allAlbumIds = trackAlbums.Select(ta => ta.AlbumId).Distinct().ToList();

        // Get all albums from database
        var allAlbums = await _unitOfWork.Albums.GetAllAsync();
        var albumsDict = allAlbums.ToDictionary(a => a.Id);

        // Identify albums to sync:
        // 1. Stubs: Label is null or empty
        // 2. Stale: LastSyncedAt < metadataThreshold
        var albumIdsToSync = allAlbumIds.Where(id =>
        {
            if (!albumsDict.TryGetValue(id, out var album))
                return false; // Album doesn't exist (shouldn't happen, but skip)

            // Sync if it's a stub (no label)
            if (string.IsNullOrEmpty(album.Label))
                return true;

            // Sync if metadata is stale
            if (album.LastSyncedAt < metadataThreshold)
                return true;

            return false;
        }).ToList();

        var totalAlbums = allAlbumIds.Count;
        var skippedCount = totalAlbums - albumIdsToSync.Count;

        _logger.LogInformation("Syncing {ToSync} albums ({Skipped} up-to-date)",
            albumIdsToSync.Count, skippedCount);

        foreach (var albumId in albumIdsToSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            try
            {
                var spotifyAlbum = await ExecuteWithRetryAsync(
                    () => _spotifyClient.Client.Albums.Get(albumId),
                    $"Album {albumId}");

                albumsDict.TryGetValue(albumId, out var existingAlbum);

                // Track if this was a stub (for counting)
                var wasStub = string.IsNullOrEmpty(existingAlbum?.Label);

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
                    albumsAdded++;
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

                    if (wasStub)
                        albumsAdded++; // Count stub enrichment as "added"
                    else
                        albumsUpdated++; // Count refresh as "updated"
                }

                if ((albumsAdded + albumsUpdated) % 10 == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    OnProgressChanged("Albums", albumsAdded + albumsUpdated, albumIdsToSync.Count,
                        $"Synced {albumsAdded + albumsUpdated} of {albumIdsToSync.Count} albums ({skippedCount} up-to-date)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch album {AlbumId}", albumId);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        OnProgressChanged("Albums", albumsAdded + albumsUpdated, albumIdsToSync.Count,
            $"Completed: {albumsAdded} added, {albumsUpdated} updated");

        _logger.LogInformation("Incremental album sync: {Added} added, {Updated} updated", albumsAdded, albumsUpdated);
        return (albumsAdded, albumsUpdated);
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
            .Where(id => !string.IsNullOrWhiteSpace(id)) // Filter out null/empty IDs
            .Take(10) // TESTING: Limit to 10 tracks for initial test
            .ToList();

        var total = trackIds.Count;

        if (total == 0)
        {
            _logger.LogInformation("No new tracks need audio features");
            return 0;
        }

        _logger.LogInformation("TESTING: Processing {Total} tracks to test if audio features API is working", total);

        // Process tracks individually - batch endpoint is deprecated
        for (var i = 0; i < trackIds.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var trackId = trackIds[i];

            try
            {
                _logger.LogInformation("Attempting to fetch audio features for track {TrackId} ({Index}/{Total})",
                    trackId, i + 1, total);

                var af = await ExecuteWithRetryAsync(
                    () => _spotifyClient.Client.Tracks.GetAudioFeatures(trackId),
                    $"Audio features for track {trackId}");

                if (af == null)
                {
                    _logger.LogWarning("Track {TrackId} returned null audio features (likely podcast/local file)", trackId);
                    continue;
                }

                _logger.LogInformation("✓ Successfully fetched audio features for track {TrackId}: Tempo={Tempo}, Energy={Energy}, Danceability={Danceability}",
                    trackId, af.Tempo, af.Energy, af.Danceability);

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

                // Save every 50 tracks
                if (audioFeaturesProcessed % 50 == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    OnProgressChanged("Audio Features", audioFeaturesProcessed, total,
                        $"Processed {audioFeaturesProcessed} of {total} audio features");
                }
            }
            catch (SpotifyAPI.Web.APIException apiEx)
            {
                // Extract detailed error information from Spotify API exception
                var statusCode = apiEx.Response?.StatusCode ?? HttpStatusCode.InternalServerError;
                var responseBody = apiEx.Response?.Body != null ? System.Text.Json.JsonSerializer.Serialize(apiEx.Response.Body) : "No response body";

                _logger.LogError(
                    "Failed to fetch audio features for track {TrackId}. " +
                    "HTTP Status: {StatusCode}, Response: {ResponseBody}, Message: {Message}. Skipping.",
                    trackId, statusCode, responseBody, apiEx.Message);

                // Continue processing other tracks instead of stopping
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch audio features for track {TrackId}. Skipping.", trackId);
                // Continue processing other tracks instead of stopping
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var failedCount = total - audioFeaturesProcessed;
        var message = failedCount > 0
            ? $"Completed: {audioFeaturesProcessed} succeeded, {failedCount} failed. Check logs for details."
            : $"Processed {audioFeaturesProcessed} of {total} audio features successfully";

        OnProgressChanged("Audio Features", audioFeaturesProcessed, total, message);

        if (failedCount > 0)
        {
            _logger.LogWarning(
                "Audio features sync completed with errors: {Succeeded} succeeded, {Failed} failed out of {Total} total. " +
                "Review error logs above for details on each failure.",
                audioFeaturesProcessed, failedCount, total);
        }
        else
        {
            _logger.LogInformation("Synced {Count} audio features successfully", audioFeaturesProcessed);
        }

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

    /// <summary>
    /// Incrementally syncs playlists using SnapshotId for change detection
    /// </summary>
    /// <returns>Tuple of (total playlists, changed playlists)</returns>
    private async Task<(int Total, int Changed)> IncrementalSyncPlaylistsAsync(CancellationToken cancellationToken)
    {
        var playlistsTotal = 0;
        var playlistsChanged = 0;
        var offset = 0;
        const int limit = 50;
        var now = DateTime.UtcNow;

        _logger.LogInformation("Checking playlists for changes (using SnapshotId)...");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync();

            var playlistsPage = await ExecuteWithRetryAsync(
                () => _spotifyClient.Client.Playlists.GetUsers(_spotifyClient.UserId!,
                    new PlaylistGetUsersRequest { Limit = limit, Offset = offset }),
                $"User playlists (offset {offset})");

            if (playlistsPage.Items == null || playlistsPage.Items.Count == 0)
                break;

            foreach (var spotifyPlaylist in playlistsPage.Items)
            {
                playlistsTotal++;

                var existingPlaylist = await _unitOfWork.Playlists.GetByIdAsync(spotifyPlaylist.Id!);
                var needsSync = false;

                if (existingPlaylist == null)
                {
                    // New playlist - needs sync
                    var newPlaylist = new Playlist
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
                    await _unitOfWork.Playlists.AddAsync(newPlaylist);
                    needsSync = true;
                    playlistsChanged++;
                }
                else if (existingPlaylist.SnapshotId != spotifyPlaylist.SnapshotId)
                {
                    // SnapshotId changed - playlist has been modified
                    existingPlaylist.Name = spotifyPlaylist.Name!;
                    existingPlaylist.Description = spotifyPlaylist.Description;
                    existingPlaylist.OwnerId = spotifyPlaylist.Owner?.Id ?? string.Empty;
                    existingPlaylist.IsPublic = spotifyPlaylist.Public ?? false;
                    existingPlaylist.SnapshotId = spotifyPlaylist.SnapshotId!;
                    existingPlaylist.LastSyncedAt = now;
                    _unitOfWork.Playlists.Update(existingPlaylist);
                    needsSync = true;
                    playlistsChanged++;
                }
                // else: SnapshotId unchanged - skip track sync

                if (needsSync)
                {
                    // Sync playlist tracks (only for new/changed playlists)
                    await SyncPlaylistTracksAsync(spotifyPlaylist.Id!, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            OnProgressChanged("Playlists", playlistsTotal, playlistsPage.Total ?? playlistsTotal,
                $"Checked {playlistsTotal} playlists, {playlistsChanged} changed");

            offset += limit;

            if (offset >= playlistsPage.Total)
                break;
        }

        _logger.LogInformation("Incremental playlist sync: {Total} total, {Changed} changed", playlistsTotal, playlistsChanged);
        return (playlistsTotal, playlistsChanged);
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
        var globalPosition = 0; // Track actual position across all pages

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
                    // Check if track exists in database, if not sync it
                    var trackExists = await _unitOfWork.Tracks.GetByIdAsync(fullTrack.Id);
                    if (trackExists == null)
                    {
                        // Track is in playlist but not in saved library - sync it now
                        _logger.LogDebug("Track {TrackId} ({TrackName}) in playlist {PlaylistId} not in saved library, syncing now",
                            fullTrack.Id, fullTrack.Name, playlistId);

                        // Sync the track
                        var track = new Track
                        {
                            Id = fullTrack.Id,
                            Name = fullTrack.Name,
                            DurationMs = fullTrack.DurationMs,
                            Explicit = fullTrack.Explicit,
                            Popularity = fullTrack.Popularity,
                            Isrc = fullTrack.ExternalIds?.ContainsKey("isrc") == true
                                ? fullTrack.ExternalIds["isrc"]
                                : null,
                            AddedAt = DateTime.UtcNow, // Playlist tracks don't have individual AddedAt for library
                            FirstSyncedAt = DateTime.UtcNow,
                            LastSyncedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Tracks.AddAsync(track);

                        // Also sync artists for this track
                        var now = DateTime.UtcNow;
                        var artistPosition = 0;
                        foreach (var artist in fullTrack.Artists)
                        {
                            // Create stub artist if doesn't exist
                            var existingArtist = await _unitOfWork.Artists.GetByIdAsync(artist.Id);
                            if (existingArtist == null)
                            {
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
                                TrackId = fullTrack.Id,
                                ArtistId = artist.Id,
                                Position = artistPosition++
                            };

                            var existingRelation = (await _unitOfWork.TrackArtists.GetAllAsync())
                                .FirstOrDefault(ta => ta.TrackId == fullTrack.Id && ta.ArtistId == artist.Id);

                            if (existingRelation == null)
                            {
                                await _unitOfWork.TrackArtists.AddAsync(trackArtist);
                            }
                        }

                        // Also sync album for this track
                        if (fullTrack.Album != null)
                        {
                            var existingAlbum = await _unitOfWork.Albums.GetByIdAsync(fullTrack.Album.Id);
                            if (existingAlbum == null)
                            {
                                var stubAlbum = new Album
                                {
                                    Id = fullTrack.Album.Id,
                                    Name = fullTrack.Album.Name,
                                    AlbumType = fullTrack.Album.AlbumType ?? "album",
                                    ReleaseDate = ParseReleaseDate(fullTrack.Album.ReleaseDate),
                                    TotalTracks = fullTrack.Album.TotalTracks,
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
                                TrackId = fullTrack.Id,
                                AlbumId = fullTrack.Album.Id,
                                DiscNumber = fullTrack.DiscNumber,
                                TrackNumber = fullTrack.TrackNumber
                            };

                            var existingAlbumRelation = (await _unitOfWork.TrackAlbums.GetAllAsync())
                                .FirstOrDefault(ta => ta.TrackId == fullTrack.Id && ta.AlbumId == fullTrack.Album.Id);

                            if (existingAlbumRelation == null)
                            {
                                await _unitOfWork.TrackAlbums.AddAsync(trackAlbum);
                            }
                        }
                    }

                    var playlistTrack = new PlaylistTrack
                    {
                        PlaylistId = playlistId,
                        TrackId = fullTrack.Id,
                        Position = globalPosition, // Use global position counter, not offset-based calculation
                        AddedAt = item.AddedAt.HasValue
                            ? DateTime.SpecifyKind(item.AddedAt.Value, DateTimeKind.Utc)
                            : DateTime.UtcNow,
                        AddedBy = item.AddedBy?.Id ?? "unknown"
                    };

                    await _unitOfWork.PlaylistTracks.AddAsync(playlistTrack);
                    globalPosition++; // Increment position for each track
                }
            }

            offset += limit;

            // Check if we've retrieved all tracks
            if (offset >= playlistTracks.Total)
                break;
        }
    }

    #region Batched Sync Methods

    public async Task<BatchSyncResult> SyncTracksBatchAsync(
        int offset,
        int batchSize,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchSyncResult
        {
            NextOffset = offset
        };

        try
        {
            await _rateLimiter.WaitAsync();

            _logger.LogInformation("Syncing tracks batch: offset {Offset}, size {BatchSize}", offset, batchSize);

            var savedTracks = await _spotifyClient.Client.Library.GetTracks(
                new LibraryTracksRequest { Limit = batchSize, Offset = offset },
                cancellationToken);

            if (savedTracks == null || savedTracks.Items == null)
            {
                result.ErrorMessage = "Failed to fetch tracks from Spotify API";
                return result;
            }

            result.TotalEstimated = savedTracks.Total;
            result.ItemsProcessed = savedTracks.Items.Count;
            result.HasMore = offset + savedTracks.Items.Count < savedTracks.Total;
            result.NextOffset = offset + savedTracks.Items.Count;

            var now = DateTime.UtcNow;
            var newItems = 0;
            var updatedItems = 0;

            foreach (var savedTrack in savedTracks.Items)
            {
                if (savedTrack?.Track == null) continue;

                var track = new Track
                {
                    Id = savedTrack.Track.Id,
                    Name = savedTrack.Track.Name,
                    DurationMs = savedTrack.Track.DurationMs,
                    Explicit = savedTrack.Track.Explicit,
                    Popularity = savedTrack.Track.Popularity,
                    Isrc = savedTrack.Track.ExternalIds?.TryGetValue("isrc", out var isrc) == true ? isrc : null,
                    AddedAt = savedTrack.AddedAt,
                    FirstSyncedAt = now,
                    LastSyncedAt = now
                };

                var existingTrack = await _unitOfWork.Tracks.GetByIdAsync(track.Id);
                if (existingTrack == null)
                {
                    await _unitOfWork.Tracks.AddAsync(track);
                    newItems++;
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
                    updatedItems++;
                }

                // Create stub artist records (minimal data - will be enriched in artist sync)
                foreach (var artist in savedTrack.Track.Artists)
                {
                    var existingArtist = await _unitOfWork.Artists.GetByIdAsync(artist.Id);
                    if (existingArtist == null)
                    {
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
                }

                // Create stub album record
                if (savedTrack.Track.Album != null)
                {
                    var existingAlbum = await _unitOfWork.Albums.GetByIdAsync(savedTrack.Track.Album.Id);
                    if (existingAlbum == null)
                    {
                        var stubAlbum = new Album
                        {
                            Id = savedTrack.Track.Album.Id,
                            Name = savedTrack.Track.Album.Name,
                            AlbumType = savedTrack.Track.Album.AlbumType,
                            ReleaseDate = ParseReleaseDate(savedTrack.Track.Album.ReleaseDate),
                            TotalTracks = savedTrack.Track.Album.TotalTracks,
                            FirstSyncedAt = now,
                            LastSyncedAt = now
                        };
                        await _unitOfWork.Albums.AddAsync(stubAlbum);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();

            result.NewItemsAdded = newItems;
            result.ItemsUpdated = updatedItems;

            progressCallback?.Invoke(result.NextOffset, result.TotalEstimated ?? 0);

            _logger.LogInformation(
                "Tracks batch complete: {New} new, {Updated} updated, {Total} processed",
                newItems, updatedItems, result.ItemsProcessed);

            return result;
        }
        catch (APITooManyRequestsException ex)
        {
            result.RateLimited = true;
            result.RateLimitResetAt = DateTime.UtcNow.AddHours(24); // Default to 24 hours
            _logger.LogWarning("Rate limit hit during tracks batch sync");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing tracks batch at offset {Offset}", offset);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<BatchSyncResult> SyncArtistsBatchAsync(
        int offset,
        int batchSize,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchSyncResult { NextOffset = offset };

        try
        {
            // Get all artists that need enrichment (stub records with no genres)
            var allArtists = await _unitOfWork.Artists.GetAllAsync();
            var stubArtists = allArtists
                .Where(a => a.Genres == null || a.Genres.Length == 0)
                .OrderBy(a => a.Id)
                .Skip(offset)
                .Take(batchSize)
                .ToList();

            result.TotalEstimated = allArtists.Count(a => a.Genres == null || a.Genres.Length == 0);
            result.ItemsProcessed = stubArtists.Count;
            result.HasMore = offset + stubArtists.Count < result.TotalEstimated;
            result.NextOffset = offset + stubArtists.Count;

            if (!stubArtists.Any())
            {
                return result;
            }

            // Fetch full artist details in batches of 50 (Spotify API limit)
            var artistIds = stubArtists.Select(a => a.Id).ToList();
            var now = DateTime.UtcNow;
            var updatedCount = 0;

            for (int i = 0; i < artistIds.Count; i += 50)
            {
                await _rateLimiter.WaitAsync();

                var batchIds = artistIds.Skip(i).Take(50).ToList();
                var artistsResponse = await _spotifyClient.Client.Artists.GetSeveral(
                    new ArtistsRequest(batchIds),
                    cancellationToken);

                foreach (var fullArtist in artistsResponse.Artists)
                {
                    var artist = stubArtists.First(a => a.Id == fullArtist.Id);
                    artist.Name = fullArtist.Name;
                    artist.Genres = fullArtist.Genres?.ToArray() ?? Array.Empty<string>();
                    artist.Popularity = fullArtist.Popularity;
                    artist.Followers = fullArtist.Followers?.Total ?? 0;
                    artist.LastSyncedAt = now;

                    _unitOfWork.Artists.Update(artist);
                    updatedCount++;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            result.ItemsUpdated = updatedCount;
            progressCallback?.Invoke(result.NextOffset, result.TotalEstimated ?? 0);

            _logger.LogInformation(
                "Artists batch complete: {Updated} enriched, {Total} processed",
                updatedCount, result.ItemsProcessed);

            return result;
        }
        catch (APITooManyRequestsException)
        {
            result.RateLimited = true;
            result.RateLimitResetAt = DateTime.UtcNow.AddHours(24);
            _logger.LogWarning("Rate limit hit during artists batch sync");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing artists batch at offset {Offset}", offset);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<BatchSyncResult> SyncAlbumsBatchAsync(
        int offset,
        int batchSize,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchSyncResult { NextOffset = offset };

        try
        {
            // Get all albums that need enrichment (stub records with no label)
            var allAlbums = await _unitOfWork.Albums.GetAllAsync();
            var stubAlbums = allAlbums
                .Where(a => string.IsNullOrEmpty(a.Label))
                .OrderBy(a => a.Id)
                .Skip(offset)
                .Take(batchSize)
                .ToList();

            result.TotalEstimated = allAlbums.Count(a => string.IsNullOrEmpty(a.Label));
            result.ItemsProcessed = stubAlbums.Count;
            result.HasMore = offset + stubAlbums.Count < result.TotalEstimated;
            result.NextOffset = offset + stubAlbums.Count;

            if (!stubAlbums.Any())
            {
                return result;
            }

            // Fetch full album details in batches of 20 (Spotify API limit)
            var albumIds = stubAlbums.Select(a => a.Id).ToList();
            var now = DateTime.UtcNow;
            var updatedCount = 0;

            for (int i = 0; i < albumIds.Count; i += 20)
            {
                await _rateLimiter.WaitAsync();

                var batchIds = albumIds.Skip(i).Take(20).ToList();
                var albumsResponse = await _spotifyClient.Client.Albums.GetSeveral(
                    new AlbumsRequest(batchIds),
                    cancellationToken);

                foreach (var fullAlbum in albumsResponse.Albums)
                {
                    var album = stubAlbums.First(a => a.Id == fullAlbum.Id);
                    album.Name = fullAlbum.Name;
                    album.Label = fullAlbum.Label;
                    album.AlbumType = fullAlbum.AlbumType;
                    album.ReleaseDate = ParseReleaseDate(fullAlbum.ReleaseDate);
                    album.TotalTracks = fullAlbum.TotalTracks;
                    album.LastSyncedAt = now;

                    _unitOfWork.Albums.Update(album);
                    updatedCount++;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            result.ItemsUpdated = updatedCount;
            progressCallback?.Invoke(result.NextOffset, result.TotalEstimated ?? 0);

            _logger.LogInformation(
                "Albums batch complete: {Updated} enriched, {Total} processed",
                updatedCount, result.ItemsProcessed);

            return result;
        }
        catch (APITooManyRequestsException)
        {
            result.RateLimited = true;
            result.RateLimitResetAt = DateTime.UtcNow.AddHours(24);
            _logger.LogWarning("Rate limit hit during albums batch sync");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing albums batch at offset {Offset}", offset);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<BatchSyncResult> SyncPlaylistsBatchAsync(
        int offset,
        int batchSize,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchSyncResult { NextOffset = offset };

        try
        {
            await _rateLimiter.WaitAsync();

            var playlists = await _spotifyClient.Client.Playlists.CurrentUsers(
                new PlaylistCurrentUsersRequest { Limit = batchSize, Offset = offset },
                cancellationToken);

            if (playlists == null || playlists.Items == null)
            {
                result.ErrorMessage = "Failed to fetch playlists from Spotify API";
                return result;
            }

            result.TotalEstimated = playlists.Total;
            result.ItemsProcessed = playlists.Items.Count;
            result.HasMore = offset + playlists.Items.Count < playlists.Total;
            result.NextOffset = offset + playlists.Items.Count;

            var now = DateTime.UtcNow;
            var newItems = 0;
            var updatedItems = 0;

            foreach (var playlist in playlists.Items)
            {
                var existingPlaylist = await _unitOfWork.Playlists.GetByIdAsync(playlist.Id);

                if (existingPlaylist == null)
                {
                    var newPlaylist = new Playlist
                    {
                        Id = playlist.Id,
                        Name = playlist.Name,
                        Description = playlist.Description,
                        IsPublic = playlist.Public ?? false,
                        SnapshotId = playlist.SnapshotId,
                        OwnerId = playlist.Owner?.Id ?? "",
                        FirstSyncedAt = now,
                        LastSyncedAt = now
                    };
                    await _unitOfWork.Playlists.AddAsync(newPlaylist);
                    newItems++;
                }
                else
                {
                    existingPlaylist.Name = playlist.Name;
                    existingPlaylist.Description = playlist.Description;
                    existingPlaylist.IsPublic = playlist.Public ?? false;
                    existingPlaylist.SnapshotId = playlist.SnapshotId;
                    existingPlaylist.OwnerId = playlist.Owner?.Id ?? "";
                    existingPlaylist.LastSyncedAt = now;

                    _unitOfWork.Playlists.Update(existingPlaylist);
                    updatedItems++;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            result.NewItemsAdded = newItems;
            result.ItemsUpdated = updatedItems;
            progressCallback?.Invoke(result.NextOffset, result.TotalEstimated ?? 0);

            _logger.LogInformation(
                "Playlists batch complete: {New} new, {Updated} updated, {Total} processed",
                newItems, updatedItems, result.ItemsProcessed);

            return result;
        }
        catch (APITooManyRequestsException)
        {
            result.RateLimited = true;
            result.RateLimitResetAt = DateTime.UtcNow.AddHours(24);
            _logger.LogWarning("Rate limit hit during playlists batch sync");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing playlists batch at offset {Offset}", offset);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    #endregion

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
