using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Analytics;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlaylistsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaylistsController> _logger;

    public PlaylistsController(
        IAnalyticsService analyticsService,
        IUnitOfWork unitOfWork,
        ILogger<PlaylistsController> logger)
    {
        _analyticsService = analyticsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get all playlists
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PlaylistDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlaylistDto>>> GetAllPlaylists()
    {
        try
        {
            var playlists = await _analyticsService.GetAllPlaylistsSortedByNameAsync();
            var playlistTracks = await _unitOfWork.PlaylistTracks.GetAllAsync();

            var playlistDtos = playlists.Select(p => new PlaylistDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                TrackCount = playlistTracks.Count(pt => pt.PlaylistId == p.Id),
                IsPublic = p.IsPublic,
                SpotifyId = null // Local playlist, no Spotify ID yet
            }).ToList();

            return Ok(playlistDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlists");
            return StatusCode(500, "An error occurred while fetching playlists");
        }
    }

    /// <summary>
    /// Get a single playlist with tracks
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PlaylistDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaylistDetailDto>> GetPlaylistById(string id)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(id);
            if (playlist == null)
            {
                return NotFound($"Playlist with ID '{id}' not found");
            }

            var tracks = await _analyticsService.GetTracksByPlaylistIdAsync(id, preserveOrder: true);
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            var trackDtos = tracks.Select(track =>
            {
                var trackArtistIds = trackArtists
                    .Where(ta => ta.TrackId == track.Id)
                    .Select(ta => ta.ArtistId)
                    .ToList();

                var trackArtistsList = artists
                    .Where(a => trackArtistIds.Contains(a.Id))
                    .Select(a => new ArtistSummaryDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Genres = a.Genres.ToList()
                    })
                    .ToList();

                var albumId = trackAlbums.FirstOrDefault(ta => ta.TrackId == track.Id)?.AlbumId;
                var album = albumId != null ? albums.FirstOrDefault(a => a.Id == albumId) : null;
                var allGenres = trackArtistsList.SelectMany(a => a.Genres).Distinct().ToList();

                return new TrackDto
                {
                    Id = track.Id,
                    Name = track.Name,
                    Artists = trackArtistsList,
                    AlbumName = album?.Name,
                    DurationMs = track.DurationMs,
                    Popularity = track.Popularity,
                    Genres = allGenres,
                    Explicit = track.Explicit,
                    AddedAt = track.AddedAt
                };
            }).ToList();

            var playlistDetail = new PlaylistDetailDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                TrackCount = tracks.Count,
                IsPublic = playlist.IsPublic,
                SpotifyId = null, // Local playlist
                Tracks = trackDtos,
                TotalDurationMs = trackDtos.Sum(t => t.DurationMs)
            };

            return Ok(playlistDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlist {PlaylistId}", id);
            return StatusCode(500, "An error occurred while fetching playlist");
        }
    }

    /// <summary>
    /// Create a new playlist
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PlaylistDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlaylistDto>> CreatePlaylist([FromBody] CreatePlaylistRequest request)
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
                SnapshotId = Guid.NewGuid().ToString()
            };

            await _unitOfWork.Playlists.AddAsync(playlist);
            await _unitOfWork.SaveChangesAsync();

            var playlistDto = new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description,
                TrackCount = 0,
                IsPublic = playlist.IsPublic,
                SpotifyId = null
            };

            return CreatedAtAction(nameof(GetPlaylistById), new { id = playlist.Id }, playlistDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            return StatusCode(500, "An error occurred while creating playlist");
        }
    }

    /// <summary>
    /// Add tracks to a playlist
    /// </summary>
    [HttpPost("{id}/tracks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddTracksToPlaylist(string id, [FromBody] AddTracksRequest request)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(id);
            if (playlist == null)
            {
                return NotFound($"Playlist with ID '{id}' not found");
            }

            var existingPlaylistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
                .Where(pt => pt.PlaylistId == id)
                .ToList();

            var maxPosition = existingPlaylistTracks.Any() 
                ? existingPlaylistTracks.Max(pt => pt.Position) 
                : -1;

            foreach (var trackId in request.TrackIds)
            {
                // Check if track exists
                var track = await _unitOfWork.Tracks.GetByIdAsync(trackId);
                if (track == null)
                {
                    _logger.LogWarning("Track {TrackId} not found, skipping", trackId);
                    continue;
                }

                // Check if already in playlist
                if (existingPlaylistTracks.Any(pt => pt.TrackId == trackId))
                {
                    _logger.LogInformation("Track {TrackId} already in playlist {PlaylistId}, skipping", trackId, id);
                    continue;
                }

                maxPosition++;
                var playlistTrack = new PlaylistTrack
                {
                    PlaylistId = id,
                    TrackId = trackId,
                    Position = maxPosition,
                    AddedAt = DateTime.UtcNow
                };

                await _unitOfWork.PlaylistTracks.AddAsync(playlistTrack);
            }

            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = $"Added {request.TrackIds.Count} tracks to playlist" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", id);
            return StatusCode(500, "An error occurred while adding tracks");
        }
    }

    /// <summary>
    /// Remove a track from a playlist
    /// </summary>
    [HttpDelete("{id}/tracks/{trackId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveTrackFromPlaylist(string id, string trackId)
    {
        try
        {
            var playlistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
                .Where(pt => pt.PlaylistId == id && pt.TrackId == trackId)
                .ToList();

            if (!playlistTracks.Any())
            {
                return NotFound($"Track '{trackId}' not found in playlist '{id}'");
            }

            foreach (var pt in playlistTracks)
            {
                _unitOfWork.PlaylistTracks.Delete(pt);
            }

            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing track {TrackId} from playlist {PlaylistId}", trackId, id);
            return StatusCode(500, "An error occurred while removing track");
        }
    }

    /// <summary>
    /// Delete a playlist
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePlaylist(string id)
    {
        try
        {
            var playlist = await _unitOfWork.Playlists.GetByIdAsync(id);
            if (playlist == null)
            {
                return NotFound($"Playlist with ID '{id}' not found");
            }

            // Delete all playlist tracks first
            var playlistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
                .Where(pt => pt.PlaylistId == id)
                .ToList();

            foreach (var pt in playlistTracks)
            {
                _unitOfWork.PlaylistTracks.Delete(pt);
            }

            // Delete the playlist
            _unitOfWork.Playlists.Delete(playlist);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist {PlaylistId}", id);
            return StatusCode(500, "An error occurred while deleting playlist");
        }
    }
}
