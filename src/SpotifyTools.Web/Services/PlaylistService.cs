using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

public class PlaylistService : IPlaylistService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(
        SpotifyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<PlaylistService> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
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
}
