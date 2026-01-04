using Microsoft.AspNetCore.Mvc;
using SpotifyTools.Analytics;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TracksController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TracksController> _logger;

    public TracksController(
        IAnalyticsService analyticsService,
        IUnitOfWork unitOfWork,
        ILogger<TracksController> logger)
    {
        _analyticsService = analyticsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of all tracks
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TrackDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TrackDto>>> GetAllTracks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = "name")
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            // Apply sorting
            var sortedTracks = sortBy?.ToLower() switch
            {
                "popularity" => tracks.OrderByDescending(t => t.Popularity),
                "addedat" => tracks.OrderByDescending(t => t.AddedAt),
                _ => tracks.OrderBy(t => t.Name)
            };

            var totalCount = tracks.Count();
            var pagedTracks = sortedTracks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(track => MapToTrackDto(track, trackArtists, artists, trackAlbums, albums))
                .ToList();

            return Ok(new PagedResult<TrackDto>
            {
                Items = pagedTracks,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tracks");
            return StatusCode(500, "An error occurred while fetching tracks");
        }
    }

    /// <summary>
    /// Search tracks by name or artist
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<TrackDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TrackDto>>> SearchTracks(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query cannot be empty");
            }

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var searchResults = await _analyticsService.SearchTracksAsync(q, limit: 1000);
            var trackIds = searchResults.Select(r => r.TrackId).ToList();

            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var filteredTracks = tracks.Where(t => trackIds.Contains(t.Id)).ToList();

            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            var totalCount = filteredTracks.Count;
            var pagedTracks = filteredTracks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(track => MapToTrackDto(track, trackArtists, artists, trackAlbums, albums))
                .ToList();

            return Ok(new PagedResult<TrackDto>
            {
                Items = pagedTracks,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tracks for query: {Query}", q);
            return StatusCode(500, "An error occurred while searching tracks");
        }
    }

    /// <summary>
    /// Get a single track by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrackDto>> GetTrackById(string id)
    {
        try
        {
            var track = await _unitOfWork.Tracks.GetByIdAsync(id);
            if (track == null)
            {
                return NotFound($"Track with ID '{id}' not found");
            }

            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            var trackDto = MapToTrackDto(track, trackArtists, artists, trackAlbums, albums);
            return Ok(trackDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching track {TrackId}", id);
            return StatusCode(500, "An error occurred while fetching track");
        }
    }

    private TrackDto MapToTrackDto(
        Domain.Entities.Track track,
        IEnumerable<Domain.Entities.TrackArtist> trackArtists,
        IEnumerable<Domain.Entities.Artist> artists,
        IEnumerable<Domain.Entities.TrackAlbum> trackAlbums,
        IEnumerable<Domain.Entities.Album> albums)
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
    }
}
