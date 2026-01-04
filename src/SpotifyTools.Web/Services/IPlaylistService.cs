using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for playlist-related operations with efficient database queries
/// </summary>
public interface IPlaylistService
{
    /// <summary>
    /// Get all playlists with track counts
    /// </summary>
    Task<List<PlaylistDto>> GetAllPlaylistsAsync();

    /// <summary>
    /// Get a single playlist with all tracks (efficiently loaded)
    /// </summary>
    Task<PlaylistDetailDto?> GetPlaylistByIdAsync(string id);

    /// <summary>
    /// Create a new playlist
    /// </summary>
    Task<PlaylistDto> CreatePlaylistAsync(CreatePlaylistRequest request);

    /// <summary>
    /// Add tracks to a playlist
    /// </summary>
    Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds);

    /// <summary>
    /// Remove a track from a playlist
    /// </summary>
    Task RemoveTrackFromPlaylistAsync(string playlistId, string trackId);

    /// <summary>
    /// Delete a playlist
    /// </summary>
    Task DeletePlaylistAsync(string playlistId);
}
