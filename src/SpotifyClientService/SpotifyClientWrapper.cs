using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace SpotifyClientService;

/// <summary>
/// Service for managing Spotify API authentication and client operations
/// </summary>
public class SpotifyClientWrapper : ISpotifyClientService
{
    private readonly IConfiguration _configuration;
    private SpotifyClient? _client;
    private string? _userId;
    private AuthorizationCodeTokenResponse? _tokenResponse;

    public SpotifyClient Client
    {
        get
        {
            if (_client == null)
                throw new InvalidOperationException("Spotify client not authenticated. Call AuthenticateAsync() first.");
            return _client;
        }
    }

    public string? UserId => _userId;

    public bool IsAuthenticated => _client != null;

    public string? RefreshToken => _tokenResponse?.RefreshToken;

    public SpotifyClientWrapper(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Authenticates with Spotify using OAuth authorization code flow
    /// </summary>
    public async Task AuthenticateAsync()
    {
        var clientId = _configuration["Spotify:ClientId"];
        var clientSecret = _configuration["Spotify:ClientSecret"];
        var redirectUri = _configuration["Spotify:RedirectUri"] ?? "http://localhost:5009/callback";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException(
                "Spotify ClientId and ClientSecret must be configured in appsettings.json");
        }

        // Extract port from redirect URI
        var uri = new Uri(redirectUri);
        var port = uri.Port;

        // Start local server for OAuth callback
        var server = new EmbedIOAuthServer(new Uri(redirectUri), port);
        await server.Start();

        var tcs = new TaskCompletionSource<string>();

        server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await server.Stop();
            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeTokenRequest(
                    clientId,
                    clientSecret,
                    response.Code,
                    new Uri(redirectUri)
                )
            );

            // Store the token response for refresh capability
            _tokenResponse = tokenResponse;

            // Create authenticator that will automatically refresh tokens
            var authenticator = new AuthorizationCodeAuthenticator(
                clientId!,
                clientSecret!,
                tokenResponse
            );

            // Configure automatic token refresh
            authenticator.TokenRefreshed += (sender, token) =>
            {
                _tokenResponse = token;
                Console.WriteLine("🔄 Access token automatically refreshed");
            };

            var clientConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(authenticator);

            _client = new SpotifyClient(clientConfig);

            // Get user profile
            var profile = await _client.UserProfile.Current();
            _userId = profile.Id;

            tcs.SetResult(tokenResponse.AccessToken);
        };

        server.ErrorReceived += async (sender, error, state) =>
        {
            await server.Stop();
            tcs.SetException(new Exception($"OAuth error: {error}"));
        };

        var loginRequest = new LoginRequest(
            new Uri(redirectUri),
            clientId,
            LoginRequest.ResponseType.Code
        )
        {
            Scope = new[]
            {
                Scopes.UserLibraryRead,
                Scopes.PlaylistModifyPublic,
                Scopes.PlaylistModifyPrivate,
                Scopes.UserReadRecentlyPlayed
            }
        };

        var authUri = loginRequest.ToUri();
        Console.WriteLine("Please authorize the application:");
        Console.WriteLine(authUri);
        Console.WriteLine("\nOpening browser...");

        // Open browser
        OpenBrowser(authUri.ToString());

        // Wait for authentication to complete
        await tcs.Task;

        Console.WriteLine("✓ Authentication successful!\n");
    }

    /// <summary>
    /// Authenticates using a stored refresh token (for background services)
    /// </summary>
    public async Task AuthenticateWithRefreshTokenAsync(string refreshToken)
    {
        var clientId = _configuration["Spotify:ClientId"];
        var clientSecret = _configuration["Spotify:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException(
                "Spotify ClientId and ClientSecret must be configured in appsettings.json");
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));
        }

        // Use the refresh token to get a new access token
        var config = SpotifyClientConfig.CreateDefault();
        var refreshResponse = await new OAuthClient(config).RequestToken(
            new AuthorizationCodeRefreshRequest(
                clientId,
                clientSecret,
                refreshToken
            )
        );

        // Convert refresh response to token response for storage
        // Note: The refresh response includes the new access token but may not include a new refresh token
        var tokenResponse = new AuthorizationCodeTokenResponse
        {
            AccessToken = refreshResponse.AccessToken,
            TokenType = refreshResponse.TokenType,
            ExpiresIn = refreshResponse.ExpiresIn,
            Scope = refreshResponse.Scope,
            RefreshToken = refreshToken, // Use the original refresh token
            CreatedAt = refreshResponse.CreatedAt
        };

        _tokenResponse = tokenResponse;

        // Create authenticator that will automatically refresh tokens
        var authenticator = new AuthorizationCodeAuthenticator(
            clientId,
            clientSecret,
            tokenResponse
        );

        // Configure automatic token refresh
        authenticator.TokenRefreshed += (sender, token) =>
        {
            _tokenResponse = token;
            Console.WriteLine("🔄 Access token automatically refreshed");
        };

        var clientConfig = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(authenticator);

        _client = new SpotifyClient(clientConfig);

        // Get user profile
        var profile = await _client.UserProfile.Current();
        _userId = profile.Id;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback for systems where UseShellExecute doesn't work
            Console.WriteLine($"Could not open browser automatically. Please open this URL manually: {url}");
        }
    }
}
