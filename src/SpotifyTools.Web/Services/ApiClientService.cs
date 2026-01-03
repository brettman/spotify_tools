using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

public class ApiClientService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(HttpClient http, ILogger<ApiClientService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<GenreDto>> GetGenresAsync()
    {
        try
        {
            var genres = await _http.GetFromJsonAsync<List<GenreDto>>("api/genres");
            return genres ?? new List<GenreDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching genres");
            throw;
        }
    }

    public async Task<PagedResult<TrackDto>> GetTracksByGenreAsync(string genreName, int page = 1, int pageSize = 50)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<PagedResult<TrackDto>>(
                $"api/genres/{Uri.EscapeDataString(genreName)}/tracks?page={page}&pageSize={pageSize}");
            return result ?? new PagedResult<TrackDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tracks for genre {Genre}", genreName);
            throw;
        }
    }

    public async Task<List<PlaylistDto>> GetPlaylistsAsync()
    {
        try
        {
            var playlists = await _http.GetFromJsonAsync<List<PlaylistDto>>("api/playlists");
            return playlists ?? new List<PlaylistDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlists");
            throw;
        }
    }

    public async Task<PlaylistDetailDto?> GetPlaylistByIdAsync(string id)
    {
        try
        {
            return await _http.GetFromJsonAsync<PlaylistDetailDto>($"api/playlists/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlist {PlaylistId}", id);
            throw;
        }
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(CreatePlaylistRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/playlists", request);
            response.EnsureSuccessStatusCode();
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDto>();
            return playlist ?? throw new Exception("Failed to create playlist");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            throw;
        }
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
    {
        try
        {
            var request = new AddTracksRequest { TrackIds = trackIds };
            var response = await _http.PostAsJsonAsync($"api/playlists/{playlistId}/tracks", request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", playlistId);
            throw;
        }
    }
}
