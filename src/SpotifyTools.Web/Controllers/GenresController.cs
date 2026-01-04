using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Web.DTOs;
using SpotifyTools.Web.Services;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;
    private readonly ILogger<GenresController> _logger;

    public GenresController(
        IGenreService genreService,
        ILogger<GenresController> logger)
    {
        _genreService = genreService;
        _logger = logger;
    }

    /// <summary>
    /// Get all genres with track and artist counts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<GenreDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GenreDto>>> GetAllGenres()
    {
        try
        {
            var genres = await _genreService.GetAllGenresAsync();
            return Ok(genres);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching genres");
            return StatusCode(500, "An error occurred while fetching genres");
        }
    }

    /// <summary>
    /// Get paginated tracks for a specific genre
    /// </summary>
    [HttpGet("{genreName}/tracks")]
    [ProducesResponseType(typeof(PagedResult<TrackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TrackDto>>> GetTracksByGenre(
        string genreName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _genreService.GetTracksByGenrePagedAsync(genreName, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tracks for genre {Genre}", genreName);
            return StatusCode(500, "An error occurred while fetching tracks");
        }
    }
}
