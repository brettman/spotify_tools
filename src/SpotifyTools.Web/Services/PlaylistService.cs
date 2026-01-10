using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Web.DTOs;
using SpotifyClientService;
using SpotifyAPI.Web;

namespace SpotifyTools.Web.Services;

public class PlaylistService : IPlaylistService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(
        SpotifyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ISpotifyClientService spotifyClient,
        ILogger<PlaylistService> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _spotifyClient = spotifyClient;
        _logger = logger;
    }

    public async Task<List<PlaylistDto>> GetAllPlaylistsAsync()
    {
        try
        {
            // EFFICIENT QUERY: Single query with projection
            var playlists = await _dbContext.Playlists
                .OrderBy(p => p.Name)
                .Select(p => new PlaylistDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    TrackCount = p.PlaylistTracks.Count,
                    IsPublic = p.IsPublic,
                    SpotifyId = null // Local playlist
                })
                .ToListAsync();

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlists");
            throw;
        }
    }

    public async Task<PlaylistDetailDto?> GetPlaylistByIdAsync(string id)
    {
        try
        {
            // EFFICIENT QUERY: Single query with all includes
            var playlist = await _dbContext.Playlists
                .Where(p => p.Id == id)
                .Select(p => new PlaylistDetailDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    TrackCount = p.PlaylistTracks.Count,
                    IsPublic = p.IsPublic,
                    SpotifyId = null,
                    Tracks = p.PlaylistTracks
                        .OrderBy(pt => pt.Position)
                        .Select(pt => new TrackDto
                        {
                            Id = pt.Track.Id,
                            Name = pt.Track.Name,
                            Artists = pt.Track.TrackArtists
                                .OrderBy(ta => ta.Position)
                                .Select(ta => new ArtistSummaryDto
                                {
                                    Id = ta.Artist.Id,
                                    Name = ta.Artist.Name,
                                    Genres = ta.Artist.Genres.ToList()
                                })
                                .ToList(),
                            AlbumName = pt.Track.TrackAlbums
                                .OrderBy(ta => ta.DiscNumber)
                                .ThenBy(ta => ta.TrackNumber)
                                .Select(ta => ta.Album.Name)
                                .FirstOrDefault(),
                            DurationMs = pt.Track.DurationMs,
                            Popularity = pt.Track.Popularity,
                            Explicit = pt.Track.Explicit,
                            AddedAt = pt.Track.AddedAt,
                            Genres = pt.Track.TrackArtists
                                .SelectMany(ta => ta.Artist.Genres)
                                .Distinct()
                                .ToList()
                        })
                        .ToList(),
                    TotalDurationMs = p.PlaylistTracks.Sum(pt => pt.Track.DurationMs)
                })
                .FirstOrDefaultAsync();

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlist {PlaylistId}", id);
            throw;
        }
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(CreatePlaylistRequest request)
    {
        try
        {
            var playlist = new Playlist
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                IsPublic = request.IsPublic,
                OwnerId = "local", // TODO: Use actual user ID when authentication is implemented
                SnapshotId = Guid.NewGuid().ToString(),
                FirstSyncedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow
            };

            await _unitOfWork.Playlists.AddAsync(playlist);
            await _unitOfWork.SaveChangesAsync();

            return new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                TrackCount = 0,
                IsPublic = playlist.IsPublic,
                SpotifyId = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            throw;
        }
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(playlistId);
            if (playlist == null)
            {
                throw new KeyNotFoundException($"Playlist with ID '{playlistId}' not found");
            }

            // Get existing playlist tracks efficiently
            var existingTrackIds = (await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .Select(pt => pt.TrackId)
                .ToListAsync())
                .ToHashSet();

            var maxPosition = await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .Select(pt => (int?)pt.Position)
                .MaxAsync() ?? -1;

            foreach (var trackId in trackIds)
            {
                // Skip if already in playlist
                if (existingTrackIds.Contains(trackId))
                {
                    _logger.LogInformation("Track {TrackId} already in playlist {PlaylistId}, skipping", trackId, playlistId);
                    continue;
                }

                // Check if track exists
                var trackExists = await _dbContext.Tracks.AnyAsync(t => t.Id == trackId);
                if (!trackExists)
                {
                    _logger.LogWarning("Track {TrackId} not found, skipping", trackId);
                    continue;
                }

                maxPosition++;
                var playlistTrack = new PlaylistTrack
                {
                    PlaylistId = playlistId,
                    TrackId = trackId,
                    Position = maxPosition,
                    AddedAt = DateTime.UtcNow,
                    AddedBy = "local" // TODO: Use actual user ID
                };

                await _unitOfWork.PlaylistTracks.AddAsync(playlistTrack);
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", playlistId);
            throw;
        }
    }

    public async Task RemoveTrackFromPlaylistAsync(string playlistId, string trackId)
    {
        try
        {
            var playlistTracks = await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId)
                .ToListAsync();

            if (!playlistTracks.Any())
            {
                throw new KeyNotFoundException($"Track '{trackId}' not found in playlist '{playlistId}'");
            }

            foreach (var pt in playlistTracks)
            {
                _unitOfWork.PlaylistTracks.Delete(pt);
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing track {TrackId} from playlist {PlaylistId}", trackId, playlistId);
            throw;
        }
    }

    public async Task DeletePlaylistAsync(string playlistId)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(playlistId);
            if (playlist == null)
            {
                throw new KeyNotFoundException($"Playlist with ID '{playlistId}' not found");
            }

            // Delete all playlist tracks first
            var playlistTracks = await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .ToListAsync();

            foreach (var pt in playlistTracks)
            {
                _unitOfWork.PlaylistTracks.Delete(pt);
            }

            // Delete the playlist
            _unitOfWork.Playlists.Delete(playlist);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist {PlaylistId}", playlistId);
            throw;
        }
    }

    public async Task<PlaylistDto> CreateAndSyncPlaylistAsync(CreatePlaylistRequest request, List<string> trackIds)
    {
        try
        {
            // Create playlist locally first
            var playlistDto = await CreatePlaylistAsync(request);

            // Add tracks BEFORE syncing (sync requires tracks to exist)
            await AddTracksToPlaylistAsync(playlistDto.Id, trackIds);

            // Sync to Spotify
            var spotifyPlaylistId = await SyncPlaylistToSpotifyAsync(playlistDto.Id);
            playlistDto.SpotifyId = spotifyPlaylistId;
            playlistDto.TrackCount = trackIds.Count;

            return playlistDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating and syncing playlist");
            throw;
        }
    }

    public async Task<string> SyncPlaylistToSpotifyAsync(string playlistId)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(playlistId);
            if (playlist == null)
            {
                throw new KeyNotFoundException($"Playlist with ID '{playlistId}' not found");
            }

            // Ensure Spotify client is authenticated
            if (!_spotifyClient.IsAuthenticated)
            {
                _logger.LogInformation("Spotify client not authenticated. Initiating authentication...");
                await _spotifyClient.AuthenticateAsync();
            }

            if (_spotifyClient.Client == null)
            {
                throw new InvalidOperationException("Spotify client failed to initialize.");
            }

            if (string.IsNullOrEmpty(_spotifyClient.UserId))
            {
                throw new InvalidOperationException("User ID is not available after authentication.");
            }

            // Get tracks for this playlist
            var playlistTracks = await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .OrderBy(pt => pt.Position)
                .Select(pt => pt.TrackId)
                .ToListAsync();

            if (!playlistTracks.Any())
            {
                throw new InvalidOperationException($"Playlist '{playlist.Name}' has no tracks to sync.");
            }

            var trackUris = playlistTracks
                .Select(trackId => $"spotify:track:{trackId}")
                .ToList();

            // Check if this is a new playlist (GUID) or existing (Spotify ID)
            bool isGuid = Guid.TryParse(playlist.Id, out _);

            if (isGuid)
            {
                // CREATE NEW PLAYLIST ON SPOTIFY
                _logger.LogInformation("Creating new Spotify playlist '{PlaylistName}' with {TrackCount} tracks",
                    playlist.Name, playlistTracks.Count);

                var playlistRequest = new PlaylistCreateRequest(playlist.Name)
                {
                    Public = playlist.IsPublic,
                    Description = playlist.Description ?? "Created from Spotify Tools"
                };

                var spotifyPlaylist = await _spotifyClient.Client.Playlists.Create(_spotifyClient.UserId, playlistRequest);

                _logger.LogInformation("Created Spotify playlist: {PlaylistId} ({PlaylistName})",
                    spotifyPlaylist.Id, spotifyPlaylist.Name);

                // Add tracks to playlist (Spotify API limit is 100 tracks per request)
                const int batchSize = 100;
                for (int i = 0; i < trackUris.Count; i += batchSize)
                {
                    var batch = trackUris.Skip(i).Take(batchSize).ToList();
                    var addRequest = new PlaylistAddItemsRequest(batch);

                    await _spotifyClient.Client.Playlists.AddItems(spotifyPlaylist.Id!, addRequest);

                    _logger.LogInformation("Added {Count} tracks to Spotify playlist (batch {BatchNum}/{TotalBatches})",
                        batch.Count, (i / batchSize) + 1, (int)Math.Ceiling(trackUris.Count / (double)batchSize));
                }

                // Update the playlist ID to the Spotify ID
                // We need to update the primary key, which requires deleting old record and creating new one
                var oldId = playlist.Id;

                // Delete old playlist-track relationships
                var oldPlaylistTracks = await _dbContext.PlaylistTracks
                    .Where(pt => pt.PlaylistId == oldId)
                    .ToListAsync();

                _dbContext.PlaylistTracks.RemoveRange(oldPlaylistTracks);

                // Delete old playlist
                _unitOfWork.Playlists.Delete(playlist);
                await _unitOfWork.SaveChangesAsync();

                // Create new playlist with Spotify ID
                var syncedPlaylist = new Playlist
                {
                    Id = spotifyPlaylist.Id!,
                    Name = spotifyPlaylist.Name!,
                    Description = playlist.Description,
                    OwnerId = _spotifyClient.UserId,
                    IsPublic = playlist.IsPublic,
                    SnapshotId = spotifyPlaylist.SnapshotId!,
                    FirstSyncedAt = playlist.FirstSyncedAt,
                    LastSyncedAt = DateTime.UtcNow
                };

                await _unitOfWork.Playlists.AddAsync(syncedPlaylist);
                await _unitOfWork.SaveChangesAsync();

                // Recreate playlist-track relationships with new ID
                for (int i = 0; i < playlistTracks.Count; i++)
                {
                    var pt = new PlaylistTrack
                    {
                        PlaylistId = spotifyPlaylist.Id!,
                        TrackId = playlistTracks[i],
                        Position = i,
                        AddedAt = DateTime.UtcNow,
                        AddedBy = _spotifyClient.UserId
                    };
                    await _unitOfWork.PlaylistTracks.AddAsync(pt);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Successfully created and synced playlist '{PlaylistName}' to Spotify with {TrackCount} tracks",
                    syncedPlaylist.Name, trackUris.Count);

                return spotifyPlaylist.Id!;
            }
            else
            {
                // UPDATE EXISTING SPOTIFY PLAYLIST
                _logger.LogInformation("Updating existing Spotify playlist '{PlaylistName}' (ID: {PlaylistId}) with {TrackCount} tracks",
                    playlist.Name, playlist.Id, playlistTracks.Count);

                // Replace all tracks in the Spotify playlist with current local tracks
                var replaceRequest = new PlaylistReplaceItemsRequest(trackUris);
                await _spotifyClient.Client.Playlists.ReplaceItems(playlist.Id, replaceRequest);

                _logger.LogInformation("Replaced all tracks in Spotify playlist '{PlaylistName}'", playlist.Name);

                // Fetch updated playlist info to get new SnapshotId
                var updatedPlaylist = await _spotifyClient.Client.Playlists.Get(playlist.Id);

                // Update local playlist metadata
                playlist.SnapshotId = updatedPlaylist.SnapshotId!;
                playlist.LastSyncedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Successfully updated Spotify playlist '{PlaylistName}' with {TrackCount} tracks",
                    playlist.Name, trackUris.Count);

                return playlist.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing playlist {PlaylistId} to Spotify", playlistId);
            throw;
        }
    }
}
