using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for listening analytics and statistics
/// </summary>
public interface IListeningAnalyticsService
{
    // Overall statistics
    Task<ListeningStatsDto> GetOverallStatsAsync(TimeRange range = TimeRange.AllTime);

    // Top lists
    Task<List<TrackPlayCountDto>> GetMostPlayedTracksAsync(int top = 50, TimeRange range = TimeRange.AllTime);
    Task<List<ArtistPlayCountDto>> GetMostPlayedArtistsAsync(int top = 50, TimeRange range = TimeRange.AllTime);
    Task<List<GenrePlayCountDto>> GetMostPlayedGenresAsync(int top = 50, TimeRange range = TimeRange.AllTime);

    // Time-based patterns
    Task<List<PlaysByDateDto>> GetPlaysByDateAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<PlaysByHourDto>> GetPlaysByHourAsync(TimeRange range = TimeRange.AllTime);
    Task<List<PlaysByDayOfWeekDto>> GetPlaysByDayOfWeekAsync(TimeRange range = TimeRange.AllTime);

    // Context analysis
    Task<List<PlaysByContextDto>> GetPlaysByContextAsync(TimeRange range = TimeRange.AllTime);

    // Recent activity
    Task<List<RecentPlayDto>> GetRecentPlaysAsync(int count = 50);

    // Individual track/artist stats
    Task<int> GetTrackPlayCountAsync(string trackId, TimeRange range = TimeRange.AllTime);
    Task<int> GetArtistPlayCountAsync(string artistId, TimeRange range = TimeRange.AllTime);
}
