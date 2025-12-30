using SpotifyAPI.Web;

namespace SpotifyClientService;

/// <summary>
/// Service interface for Spotify API operations
/// </summary>
public interface ISpotifyClientService
{
    /// <summary>
    /// Authenticates with Spotify using OAuth and initializes the client
    /// </summary>
    Task AuthenticateAsync();

    /// <summary>
    /// Gets the authenticated Spotify client
    /// </summary>
    SpotifyClient Client { get; }

    /// <summary>
    /// Gets the current user's Spotify ID
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Whether the service has been authenticated
    /// </summary>
    bool IsAuthenticated { get; }
}
