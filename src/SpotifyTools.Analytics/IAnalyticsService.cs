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

    /// <summary>
    /// Gets all unique genres from all artists, sorted alphabetically
    /// </summary>
    /// <returns>List of unique genre names with artist count</returns>
    Task<List<(string Genre, int ArtistCount)>> GetAllGenresAsync();

    /// <summary>
    /// Gets all artists that have a specific genre
    /// </summary>
    /// <param name="genre">Genre name to filter by</param>
    /// <returns>List of artists with the specified genre, ordered by followers</returns>
    Task<List<Artist>> GetArtistsByGenreAsync(string genre);

    /// <summary>
    /// Gets a comprehensive genre analysis report
    /// </summary>
    /// <returns>Genre analysis with distribution, track counts, and complexity metrics</returns>
    Task<GenreAnalysisReport> GetGenreAnalysisReportAsync();

    /// <summary>
    /// Gets tracks organized by their primary genres (from artists)
    /// </summary>
    /// <returns>Dictionary of genre to track list</returns>
    Task<Dictionary<string, List<Track>>> GetTracksByGenreAsync();

    /// <summary>
    /// Gets available genre seeds from Spotify API
    /// </summary>
    /// <returns>List of genre seed strings</returns>
    Task<List<string>> GetAvailableGenreSeedsAsync();

    /// <summary>
    /// Suggests genre clusters based on overlap analysis and track counts
    /// </summary>
    /// <param name="minTracksPerCluster">Minimum number of tracks required for a cluster (default 20)</param>
    /// <returns>List of suggested genre clusters</returns>
    Task<List<GenreCluster>> SuggestGenreClustersAsync(int minTracksPerCluster = 20);

    /// <summary>
    /// Gets a detailed report of tracks that would be in a cluster-based playlist
    /// </summary>
    /// <param name="cluster">The genre cluster to generate a report for</param>
    /// <returns>Playlist report with track details</returns>
    Task<ClusterPlaylistReport> GetClusterPlaylistReportAsync(GenreCluster cluster);

    /// <summary>
    /// Saves a refined genre cluster to the database
    /// </summary>
    /// <param name="cluster">The cluster to save</param>
    /// <param name="customName">Optional custom name (uses cluster.Name if not provided)</param>
    /// <returns>The saved cluster ID</returns>
    Task<int> SaveClusterAsync(GenreCluster cluster, string? customName = null);

    /// <summary>
    /// Gets all saved clusters ordered by creation date
    /// </summary>
    /// <returns>List of saved clusters</returns>
    Task<List<GenreCluster>> GetSavedClustersAsync();

    /// <summary>
    /// Gets a saved cluster by ID
    /// </summary>
    /// <param name="id">The cluster ID</param>
    /// <returns>The saved cluster or null if not found</returns>
    Task<GenreCluster?> GetSavedClusterByIdAsync(int id);

    /// <summary>
    /// Updates a saved cluster
    /// </summary>
    /// <param name="id">The cluster ID to update</param>
    /// <param name="cluster">Updated cluster data</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateClusterAsync(int id, GenreCluster cluster);

    /// <summary>
    /// Deletes a saved cluster
    /// </summary>
    /// <param name="id">The cluster ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteClusterAsync(int id);

    /// <summary>
    /// Marks a cluster as finalized and ready for playlist generation
    /// </summary>
    /// <param name="id">The cluster ID</param>
    /// <returns>True if finalized successfully</returns>
    Task<bool> FinalizeClusterAsync(int id);
}
