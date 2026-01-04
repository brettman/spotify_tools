using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Web.DTOs;
using SpotifyTools.Web.Services;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IListeningAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IListeningAnalyticsService analyticsService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Get overall listening statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ListeningStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListeningStatsDto>> GetOverallStats([FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var stats = await _analyticsService.GetOverallStatsAsync(range);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching overall stats");
            return StatusCode(500, "An error occurred while fetching statistics");
        }
    }

    /// <summary>
    /// Get most played tracks
    /// </summary>
    [HttpGet("top-tracks")]
    [ProducesResponseType(typeof(List<TrackPlayCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrackPlayCountDto>>> GetMostPlayedTracks(
        [FromQuery] int top = 50,
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var tracks = await _analyticsService.GetMostPlayedTracksAsync(top, range);
            return Ok(tracks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching most played tracks");
            return StatusCode(500, "An error occurred while fetching tracks");
        }
    }

    /// <summary>
    /// Get most played artists
    /// </summary>
    [HttpGet("top-artists")]
    [ProducesResponseType(typeof(List<ArtistPlayCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ArtistPlayCountDto>>> GetMostPlayedArtists(
        [FromQuery] int top = 50,
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var artists = await _analyticsService.GetMostPlayedArtistsAsync(top, range);
            return Ok(artists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching most played artists");
            return StatusCode(500, "An error occurred while fetching artists");
        }
    }

    /// <summary>
    /// Get most played genres
    /// </summary>
    [HttpGet("top-genres")]
    [ProducesResponseType(typeof(List<GenrePlayCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GenrePlayCountDto>>> GetMostPlayedGenres(
        [FromQuery] int top = 50,
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var genres = await _analyticsService.GetMostPlayedGenresAsync(top, range);
            return Ok(genres);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching most played genres");
            return StatusCode(500, "An error occurred while fetching genres");
        }
    }

    /// <summary>
    /// Get plays by date
    /// </summary>
    [HttpGet("plays-by-date")]
    [ProducesResponseType(typeof(List<PlaysByDateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlaysByDateDto>>> GetPlaysByDate(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var plays = await _analyticsService.GetPlaysByDateAsync(startDate, endDate);
            return Ok(plays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching plays by date");
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get listening patterns by hour of day
    /// </summary>
    [HttpGet("plays-by-hour")]
    [ProducesResponseType(typeof(List<PlaysByHourDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlaysByHourDto>>> GetPlaysByHour(
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var plays = await _analyticsService.GetPlaysByHourAsync(range);
            return Ok(plays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching plays by hour");
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get listening patterns by day of week
    /// </summary>
    [HttpGet("plays-by-day")]
    [ProducesResponseType(typeof(List<PlaysByDayOfWeekDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlaysByDayOfWeekDto>>> GetPlaysByDayOfWeek(
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var plays = await _analyticsService.GetPlaysByDayOfWeekAsync(range);
            return Ok(plays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching plays by day of week");
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get plays by context type (playlist, album, etc.)
    /// </summary>
    [HttpGet("plays-by-context")]
    [ProducesResponseType(typeof(List<PlaysByContextDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlaysByContextDto>>> GetPlaysByContext(
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var plays = await _analyticsService.GetPlaysByContextAsync(range);
            return Ok(plays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching plays by context");
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get recent play activity
    /// </summary>
    [HttpGet("recent-plays")]
    [ProducesResponseType(typeof(List<RecentPlayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RecentPlayDto>>> GetRecentPlays([FromQuery] int count = 50)
    {
        try
        {
            var plays = await _analyticsService.GetRecentPlaysAsync(count);
            return Ok(plays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent plays");
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get play count for a specific track
    /// </summary>
    [HttpGet("tracks/{trackId}/plays")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetTrackPlayCount(
        string trackId,
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var count = await _analyticsService.GetTrackPlayCountAsync(trackId, range);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching track play count for {TrackId}", trackId);
            return StatusCode(500, "An error occurred while fetching data");
        }
    }

    /// <summary>
    /// Get play count for a specific artist
    /// </summary>
    [HttpGet("artists/{artistId}/plays")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetArtistPlayCount(
        string artistId,
        [FromQuery] TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var count = await _analyticsService.GetArtistPlayCountAsync(artistId, range);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artist play count for {ArtistId}", artistId);
            return StatusCode(500, "An error occurred while fetching data");
        }
    }
}
