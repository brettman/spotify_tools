using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Web.DTOs;
using SpotifyTools.Web.Services;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlaylistsController : ControllerBase
{
    private readonly IPlaylistService _playlistService;
    private readonly ILogger<PlaylistsController> _logger;

    public PlaylistsController(
        IPlaylistService playlistService,
        ILogger<PlaylistsController> logger)
    {
        _playlistService = playlistService;
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
            var playlists = await _playlistService.GetAllPlaylistsAsync();
            return Ok(playlists);
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
            var playlist = await _playlistService.GetPlaylistByIdAsync(id);
            if (playlist == null)
            {
                return NotFound($"Playlist with ID '{id}' not found");
            }

            return Ok(playlist);
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
            var playlist = await _playlistService.CreatePlaylistAsync(request);
            return CreatedAtAction(nameof(GetPlaylistById), new { id = playlist.Id }, playlist);
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
            await _playlistService.AddTracksToPlaylistAsync(id, request.TrackIds);
            return Ok(new { message = $"Added {request.TrackIds.Count} tracks to playlist" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            await _playlistService.RemoveTrackFromPlaylistAsync(id, trackId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            await _playlistService.DeletePlaylistAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist {PlaylistId}", id);
            return StatusCode(500, "An error occurred while deleting playlist");
        }
    }
}
