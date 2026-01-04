using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for genre-related operations with efficient database queries
/// </summary>
public interface IGenreService
{
    /// <summary>
    /// Get all genres with track and artist counts
    /// </summary>
    Task<List<GenreDto>> GetAllGenresAsync();

    /// <summary>
    /// Get paginated tracks for a specific genre with efficient database query
    /// </summary>
    Task<PagedResult<TrackDto>> GetTracksByGenrePagedAsync(
        string genreName,
        int page = 1,
        int pageSize = 50);
}
