using Microsoft.EntityFrameworkCore;
using SpotifyTools.Analytics;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

public class GenreService : IGenreService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<GenreService> _logger;

    public GenreService(
        SpotifyDbContext dbContext,
        IAnalyticsService analyticsService,
        ILogger<GenreService> logger)
    {
        _dbContext = dbContext;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<List<GenreDto>> GetAllGenresAsync()
    {
        try
        {
            // Use existing analytics service for genre list with artist counts
            var genres = await _analyticsService.GetAllGenresAsync();
            var genreDtos = genres.Select(g => new GenreDto
            {
                Name = g.Genre,
                ArtistCount = g.ArtistCount,
                TrackCount = 0 // Will be calculated below
            }).ToList();

            // Get track counts per genre efficiently
            var tracksByGenre = await _analyticsService.GetTracksByGenreAsync();
            foreach (var genreDto in genreDtos)
            {
                if (tracksByGenre.TryGetValue(genreDto.Name, out var tracks))
                {
                    genreDto.TrackCount = tracks.Count;
                }
            }

            return genreDtos.OrderByDescending(g => g.TrackCount).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching genres");
            throw;
        }
    }

    public async Task<PagedResult<TrackDto>> GetTracksByGenrePagedAsync(
        string genreName,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            // EFFICIENT QUERY: Single database query with projection
            // This replaces the N+1 query anti-pattern
            var query = _dbContext.Tracks
                .Where(t => t.TrackArtists.Any(ta => ta.Artist.Genres.Contains(genreName)))
                .OrderBy(t => t.Name);

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Get paginated tracks with all related data in single query
            var tracks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TrackDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Artists = t.TrackArtists
                        .OrderBy(ta => ta.Position)
                        .Select(ta => new ArtistSummaryDto
                        {
                            Id = ta.Artist.Id,
                            Name = ta.Artist.Name,
                            Genres = ta.Artist.Genres.ToList()
                        })
                        .ToList(),
                    AlbumName = t.TrackAlbums.OrderBy(ta => ta.DiscNumber).ThenBy(ta => ta.TrackNumber)
                        .Select(ta => ta.Album.Name)
                        .FirstOrDefault(),
                    DurationMs = t.DurationMs,
                    Popularity = t.Popularity,
                    Explicit = t.Explicit,
                    AddedAt = t.AddedAt,
                    Genres = t.TrackArtists
                        .SelectMany(ta => ta.Artist.Genres)
                        .Distinct()
                        .ToList()
                })
                .ToListAsync();

            return new PagedResult<TrackDto>
            {
                Items = tracks,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tracks for genre {Genre}", genreName);
            throw;
        }
    }
}
