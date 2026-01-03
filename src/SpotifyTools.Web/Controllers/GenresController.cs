using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Analytics;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GenresController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenresController> _logger;

    public GenresController(
        IAnalyticsService analyticsService,
        IUnitOfWork unitOfWork,
        ILogger<GenresController> logger)
    {
        _analyticsService = analyticsService;
        _unitOfWork = unitOfWork;
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
            var genres = await _analyticsService.GetAllGenresAsync();
            var genreDtos = genres.Select(g => new GenreDto
            {
                Name = g.Genre,
                ArtistCount = g.ArtistCount,
                TrackCount = 0 // Will be calculated below
            }).ToList();

            // Get track counts per genre
            var tracksByGenre = await _analyticsService.GetTracksByGenreAsync();
            foreach (var genreDto in genreDtos)
            {
                if (tracksByGenre.TryGetValue(genreDto.Name, out var tracks))
                {
                    genreDto.TrackCount = tracks.Count;
                }
            }

            return Ok(genreDtos.OrderByDescending(g => g.TrackCount).ToList());
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
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var tracksByGenre = await _analyticsService.GetTracksByGenreAsync();
            
            if (!tracksByGenre.TryGetValue(genreName, out var tracks))
            {
                return NotFound($"Genre '{genreName}' not found");
            }

            // Get all artists and track-artist relationships for mapping
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            var totalCount = tracks.Count;
            var pagedTracks = tracks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(track =>
                {
                    // Get artists for this track
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

                    // Get album for this track
                    var albumId = trackAlbums.FirstOrDefault(ta => ta.TrackId == track.Id)?.AlbumId;
                    var album = albumId != null ? albums.FirstOrDefault(a => a.Id == albumId) : null;

                    // Collect all genres from artists
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
                })
                .ToList();

            var result = new PagedResult<TrackDto>
            {
                Items = pagedTracks,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tracks for genre {Genre}", genreName);
            return StatusCode(500, "An error occurred while fetching tracks");
        }
    }
}
