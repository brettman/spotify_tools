using SpotifyTools.Domain.Entities;

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

    /// <summary>
    /// Gets all artists sorted by follower count (descending)
    /// </summary>
    /// <returns>List of all artists ordered by popularity</returns>
    Task<List<Artist>> GetAllArtistsSortedByPopularityAsync();

    /// <summary>
    /// Gets all tracks for a specific artist
    /// </summary>
    /// <param name="artistId">Spotify artist ID</param>
    /// <returns>List of tracks by the artist, ordered by popularity</returns>
    Task<List<Track>> GetTracksByArtistIdAsync(string artistId);

    /// <summary>
    /// Gets all playlists sorted by name
    /// </summary>
    /// <returns>List of all playlists ordered alphabetically</returns>
    Task<List<Playlist>> GetAllPlaylistsSortedByNameAsync();

    /// <summary>
    /// Gets all tracks in a specific playlist
    /// </summary>
    /// <param name="playlistId">Spotify playlist ID</param>
    /// <param name="preserveOrder">If true, maintains playlist track order; if false, sorts by popularity</param>
    /// <returns>List of tracks in the playlist</returns>
    Task<List<Track>> GetTracksByPlaylistIdAsync(string playlistId, bool preserveOrder = true);
}
