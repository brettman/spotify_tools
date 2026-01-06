using Microsoft.EntityFrameworkCore;
using SpotifyAPI.Web;
using SpotifyClientService;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.PlaybackWorker;

/// <summary>
/// Background service that continuously tracks Spotify playback history
/// </summary>
public class PlaybackTracker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ILogger<PlaybackTracker> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _pollingInterval;
    private readonly bool _enableErrorAlerts;
    private readonly int _maxConsecutiveErrors;
    private int _consecutiveErrorCount = 0;
    private DateTime? _lastSuccessfulPoll;

    public PlaybackTracker(
        IServiceProvider serviceProvider,
        ISpotifyClientService spotifyClient,
        ILogger<PlaybackTracker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _spotifyClient = spotifyClient;
        _logger = logger;
        _configuration = configuration;

        var intervalMinutes = configuration.GetValue<int?>("PlaybackTracking:PollingIntervalMinutes") ?? 10;
        _pollingInterval = TimeSpan.FromMinutes(intervalMinutes);
        _enableErrorAlerts = configuration.GetValue<bool>("PlaybackTracking:EnableErrorAlerts");
        _maxConsecutiveErrors = configuration.GetValue<int>("PlaybackTracking:MaxConsecutiveErrors");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Playback Tracker starting. Polling interval: {Interval} minutes",
            _pollingInterval.TotalMinutes);

        // Try to authenticate on startup
        try
        {
            await EnsureAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate on startup. Will retry on first poll.");
        }

        // Wait a bit before first poll
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndTrackPlaybackAsync(stoppingToken);
                _consecutiveErrorCount = 0; // Reset error count on success
                _lastSuccessfulPoll = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback tracking service is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrorCount++;
                _logger.LogError(ex, "Error in playback tracking poll (consecutive errors: {Count})",
                    _consecutiveErrorCount);

                if (_enableErrorAlerts && _consecutiveErrorCount >= _maxConsecutiveErrors)
                {
                    await RaiseErrorAlert(ex);
                }
            }

            // Wait before next poll
            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback tracking service is shutting down");
                break;
            }
        }

        _logger.LogInformation("Playback Tracker stopped");
    }

    private async Task PollAndTrackPlaybackAsync(CancellationToken cancellationToken)
    {
        // Ensure we're authenticated
        if (!_spotifyClient.IsAuthenticated)
        {
            _logger.LogWarning("Not authenticated. Attempting to authenticate...");
            await EnsureAuthenticatedAsync();

            if (!_spotifyClient.IsAuthenticated)
            {
                _logger.LogWarning("Authentication failed. Skipping this poll.");
                return;
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpotifyDbContext>();

        // Get the last play timestamp from database
        var lastSync = await dbContext.PlayHistories
            .OrderByDescending(ph => ph.PlayedAt)
            .Select(ph => (DateTime?)ph.PlayedAt)
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogInformation("Fetching recently played tracks (after: {LastSync})",
            lastSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? "all time");

        // Get recently played from Spotify
        CursorPaging<PlayHistoryItem>? recentlyPlayed;

        if (lastSync.HasValue)
        {
            var afterTimestamp = new DateTimeOffset(lastSync.Value).ToUnixTimeMilliseconds();
            var request = new PlayerRecentlyPlayedRequest
            {
                Limit = 50,
                After = afterTimestamp
            };
            recentlyPlayed = await _spotifyClient.Client.Player.GetRecentlyPlayed(request);
        }
        else
        {
            var request = new PlayerRecentlyPlayedRequest { Limit = 50 };
            recentlyPlayed = await _spotifyClient.Client.Player.GetRecentlyPlayed(request);
        }

        if (recentlyPlayed?.Items == null || !recentlyPlayed.Items.Any())
        {
            _logger.LogInformation("No new recently played tracks");
            return;
        }

        _logger.LogInformation("Found {Count} recently played tracks", recentlyPlayed.Items.Count);

        // Get track IDs from recently played
        var trackIds = recentlyPlayed.Items
            .Where(item => item.Track != null)
            .Select(item => item.Track!.Id)
            .Distinct()
            .ToList();

        // Check which tracks exist in the database
        var existingTrackIds = await dbContext.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var existingTrackSet = existingTrackIds.ToHashSet();

        // Save any new tracks from recently played
        var newTracksCount = 0;
        foreach (var item in recentlyPlayed.Items)
        {
            if (item.Track == null) continue;

            if (!existingTrackSet.Contains(item.Track.Id))
            {
                await SaveTrackFromApiAsync(dbContext, item.Track, cancellationToken);
                existingTrackSet.Add(item.Track.Id);
                newTracksCount++;
            }
        }

        if (newTracksCount > 0)
        {
            _logger.LogInformation("Saved {Count} new tracks from recently played", newTracksCount);
        }

        // Convert to PlayHistory entities
        var playHistories = new List<PlayHistory>();

        foreach (var item in recentlyPlayed.Items)
        {
            if (item.Track == null)
            {
                _logger.LogWarning("Recently played item has null track, skipping");
                continue;
            }

            var playHistory = new PlayHistory
            {
                Id = Guid.NewGuid().ToString(),
                TrackId = item.Track.Id,
                PlayedAt = item.PlayedAt,
                ContextType = item.Context?.Type,
                ContextUri = item.Context?.Uri,
                CreatedAt = DateTime.UtcNow
            };

            playHistories.Add(playHistory);
        }

        // Save to database with duplicate detection
        if (playHistories.Any())
        {
            var saved = await SavePlayHistoryBatchAsync(dbContext, playHistories, cancellationToken);
            _logger.LogInformation("Successfully saved {Count} new play history records", saved);
        }
        else
        {
            _logger.LogInformation("No new play history to save");
        }
    }

    private async Task<int> SavePlayHistoryBatchAsync(
        SpotifyDbContext dbContext,
        List<PlayHistory> playHistories,
        CancellationToken cancellationToken)
    {
        // Check for duplicates based on TrackId + PlayedAt combination
        var existingPlays = await dbContext.PlayHistories
            .Where(ph => playHistories.Select(p => p.TrackId).Contains(ph.TrackId))
            .Select(ph => new { ph.TrackId, ph.PlayedAt })
            .ToListAsync(cancellationToken);

        var existingPlaySet = existingPlays
            .Select(ep => $"{ep.TrackId}_{ep.PlayedAt:yyyy-MM-ddTHH:mm:ss}")
            .ToHashSet();

        // Filter out duplicates
        var newPlays = playHistories
            .Where(ph => !existingPlaySet.Contains($"{ph.TrackId}_{ph.PlayedAt:yyyy-MM-ddTHH:mm:ss}"))
            .ToList();

        if (!newPlays.Any())
        {
            _logger.LogInformation("No new play history records to save (all duplicates)");
            return 0;
        }

        await dbContext.PlayHistories.AddRangeAsync(newPlays, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newPlays.Count;
    }

    private async Task SaveTrackFromApiAsync(
        SpotifyDbContext dbContext,
        FullTrack spotifyTrack,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Saving new track {TrackId} - {TrackName}", spotifyTrack.Id, spotifyTrack.Name);

        // Create track entity
        var track = new Track
        {
            Id = spotifyTrack.Id,
            Name = spotifyTrack.Name,
            DurationMs = spotifyTrack.DurationMs,
            Explicit = spotifyTrack.Explicit,
            Popularity = spotifyTrack.Popularity,
            Isrc = spotifyTrack.ExternalIds?.ContainsKey("isrc") == true
                ? spotifyTrack.ExternalIds["isrc"]
                : null,
            AddedAt = null, // Not from user's library
            FirstSyncedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow
        };

        await dbContext.Tracks.AddAsync(track, cancellationToken);

        // Save artists and track-artist relationships
        if (spotifyTrack.Artists != null && spotifyTrack.Artists.Any())
        {
            foreach (var spotifyArtist in spotifyTrack.Artists)
            {
                // Check if artist exists
                var artistExists = await dbContext.Artists
                    .AnyAsync(a => a.Id == spotifyArtist.Id, cancellationToken);

                if (!artistExists)
                {
                    // Create minimal artist record (will be enriched by future sync)
                    var artist = new Artist
                    {
                        Id = spotifyArtist.Id,
                        Name = spotifyArtist.Name,
                        Popularity = 0, // Not available in simple artist object
                        FirstSyncedAt = DateTime.UtcNow,
                        LastSyncedAt = DateTime.UtcNow
                    };
                    await dbContext.Artists.AddAsync(artist, cancellationToken);
                }

                // Create track-artist relationship
                var trackArtist = new TrackArtist
                {
                    TrackId = spotifyTrack.Id,
                    ArtistId = spotifyArtist.Id
                };
                await dbContext.Set<TrackArtist>().AddAsync(trackArtist, cancellationToken);
            }
        }

        // Save album and track-album relationship
        if (spotifyTrack.Album != null)
        {
            // Check if album exists
            var albumExists = await dbContext.Albums
                .AnyAsync(a => a.Id == spotifyTrack.Album.Id, cancellationToken);

            if (!albumExists)
            {
                // Parse release date (Spotify returns "YYYY-MM-DD", "YYYY-MM", or "YYYY")
                DateTime? releaseDate = null;
                if (!string.IsNullOrEmpty(spotifyTrack.Album.ReleaseDate))
                {
                    if (DateTime.TryParse(spotifyTrack.Album.ReleaseDate, out var parsedDate))
                    {
                        // PostgreSQL requires UTC DateTimes
                        releaseDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                    }
                }

                // Create minimal album record (will be enriched by future sync)
                var album = new Album
                {
                    Id = spotifyTrack.Album.Id,
                    Name = spotifyTrack.Album.Name,
                    AlbumType = spotifyTrack.Album.AlbumType,
                    ReleaseDate = releaseDate,
                    TotalTracks = spotifyTrack.Album.TotalTracks,
                    FirstSyncedAt = DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow
                };
                await dbContext.Albums.AddAsync(album, cancellationToken);
            }

            // Create track-album relationship
            var trackAlbum = new TrackAlbum
            {
                TrackId = spotifyTrack.Id,
                AlbumId = spotifyTrack.Album.Id
            };
            await dbContext.Set<TrackAlbum>().AddAsync(trackAlbum, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (_spotifyClient.IsAuthenticated)
            return;

        _logger.LogInformation("Not authenticated. Attempting to authenticate...");

        // Load refresh token from database
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpotifyDbContext>();

        var token = await dbContext.SpotifyTokens
            .OrderByDescending(t => t.LastUsedAt)
            .FirstOrDefaultAsync();

        if (token == null || string.IsNullOrEmpty(token.EncryptedRefreshToken))
        {
            // No stored token - need to authenticate interactively
            _logger.LogWarning("No stored authentication token found. Starting interactive authentication...");
            await PerformInteractiveAuthenticationAsync(dbContext);
            return;
        }

        // Try to authenticate with stored refresh token
        try
        {
            _logger.LogInformation("Authenticating with stored refresh token for user {User}", token.UserIdentifier);
            await _spotifyClient.AuthenticateWithRefreshTokenAsync(token.EncryptedRefreshToken);

            // Update last used timestamp
            token.LastUsedAt = DateTime.UtcNow;
            dbContext.SpotifyTokens.Update(token);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("✓ Successfully authenticated as user {User}", _spotifyClient.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with stored refresh token. Token may have expired.");
            _logger.LogWarning("Starting interactive authentication...");
            
            // Token expired or invalid - do interactive auth
            await PerformInteractiveAuthenticationAsync(dbContext);
        }
    }

    private async Task PerformInteractiveAuthenticationAsync(SpotifyDbContext dbContext)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  SPOTIFY AUTHENTICATION REQUIRED");
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("");
        _logger.LogInformation("This service needs to authenticate with Spotify.");
        _logger.LogInformation("A browser window will open automatically.");
        _logger.LogInformation("");

        try
        {
            // Perform OAuth flow (opens browser)
            await _spotifyClient.AuthenticateAsync();

            if (!_spotifyClient.IsAuthenticated || string.IsNullOrEmpty(_spotifyClient.RefreshToken))
            {
                throw new InvalidOperationException("Authentication completed but no refresh token was obtained");
            }

            // Save the refresh token to database
            var token = new SpotifyToken
            {
                // Id is auto-generated by database
                UserIdentifier = _spotifyClient.UserId ?? "unknown",
                EncryptedRefreshToken = _spotifyClient.RefreshToken,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };

            // Remove any existing tokens for this user
            var existingTokens = dbContext.SpotifyTokens
                .Where(t => t.UserIdentifier == token.UserIdentifier)
                .ToList();
            
            if (existingTokens.Any())
            {
                _logger.LogInformation("Removing {Count} existing token(s) for user {User}", 
                    existingTokens.Count, token.UserIdentifier);
                dbContext.SpotifyTokens.RemoveRange(existingTokens);
            }

            await dbContext.SpotifyTokens.AddAsync(token);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("");
            _logger.LogInformation("✓ Authentication successful!");
            _logger.LogInformation("✓ Refresh token saved to database");
            _logger.LogInformation("✓ Future runs will authenticate automatically");
            _logger.LogInformation("");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive authentication failed");
            _logger.LogError("");
            _logger.LogError("═══════════════════════════════════════════════════════════");
            _logger.LogError("  AUTHENTICATION FAILED");
            _logger.LogError("═══════════════════════════════════════════════════════════");
            _logger.LogError("");
            _logger.LogError("The service cannot continue without authentication.");
            _logger.LogError("Please check:");
            _logger.LogError("  1. Spotify ClientId and ClientSecret are correct in appsettings.json");
            _logger.LogError("  2. RedirectUri matches Spotify Developer Dashboard settings");
            _logger.LogError("  3. Network connectivity is working");
            _logger.LogError("");
            
            throw new InvalidOperationException(
                "Failed to authenticate with Spotify. Cannot continue without valid credentials.", ex);
        }
    }

    private async Task RaiseErrorAlert(Exception ex)
    {
        var alertMessage = $@"
ALERT: Spotify Playback Worker Service Error

The playback tracking service has encountered {_consecutiveErrorCount} consecutive errors.

Last successful poll: {_lastSuccessfulPoll?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}
Current time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}

Error: {ex.Message}

Stack Trace:
{ex.StackTrace}
";

        _logger.LogCritical(alertMessage);

        // TODO: Implement additional alert mechanisms:
        // - Send email via SMTP
        // - Post to Slack webhook
        // - System notification (macOS notification center, etc.)
        // - Write to a dedicated alerts log file

        // For now, we're relying on log monitoring
        // You can add email/Slack integration here based on your needs

        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Playback Tracker is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Playback Tracker stopped gracefully");
    }
}
