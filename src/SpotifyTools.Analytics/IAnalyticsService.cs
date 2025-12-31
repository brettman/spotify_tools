namespace SpotifyTools.Analytics;

/// <summary>
/// Service for analyzing and reporting on Spotify library data
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets a detailed report for a specific track by ID
    /// </summary>
    /// <param name="trackId">Spotify track ID</param>
    /// <returns>Track detail report or null if not found</returns>
    Task<TrackDetailReport?> GetTrackDetailReportAsync(string trackId);

    /// <summary>
    /// Searches for tracks by name and returns matching track IDs
    /// </summary>
    /// <param name="searchTerm">Track name to search for</param>
    /// <param name="limit">Maximum number of results (default 10)</param>
    /// <returns>List of matching tracks with ID and display name</returns>
    Task<List<(string TrackId, string DisplayName)>> SearchTracksAsync(string searchTerm, int limit = 10);
}
