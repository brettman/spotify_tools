using Microsoft.Extensions.DependencyInjection;
using SpotifyAPI.Web;
using SpotifyClientService;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Background service that continuously polls Spotify for recently played tracks
/// </summary>
public class PlaybackTrackingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ILogger<PlaybackTrackingService> _logger;
    private readonly TimeSpan _pollingInterval;

    public PlaybackTrackingService(
        IServiceProvider serviceProvider,
        ISpotifyClientService spotifyClient,
        ILogger<PlaybackTrackingService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _spotifyClient = spotifyClient;
        _logger = logger;

        // Get polling interval from configuration, default to 10 minutes
        var intervalMinutes = configuration.GetValue<int?>("PlaybackTracking:PollingIntervalMinutes") ?? 10;
        _pollingInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Playback Tracking Service started. Polling interval: {Interval} minutes",
            _pollingInterval.TotalMinutes);

        // Wait a bit before first poll to allow application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only poll if authenticated
                if (!_spotifyClient.IsAuthenticated)
                {
                    _logger.LogWarning("Spotify client not authenticated. Skipping playback tracking poll.");
                    await Task.Delay(_pollingInterval, stoppingToken);
                    continue;
                }

                await PollRecentlyPlayedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback tracking service is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in playback tracking poll");
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

        _logger.LogInformation("Playback Tracking Service stopped");
    }

    private async Task PollRecentlyPlayedAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Create a scope for scoped services
            using var scope = _serviceProvider.CreateScope();
            var playHistoryService = scope.ServiceProvider.GetRequiredService<IPlayHistoryService>();

            // Get the last play timestamp from our database
            var lastSync = await playHistoryService.GetLastPlayTimestampAsync();

            _logger.LogInformation("Fetching recently played tracks (after: {LastSync})",
                lastSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? "all time");

            // Get recently played from Spotify
            CursorPaging<PlayHistoryItem>? recentlyPlayed;

            if (lastSync.HasValue)
            {
                // Spotify expects Unix timestamp in milliseconds
                var afterTimestamp = new DateTimeOffset(lastSync.Value).ToUnixTimeMilliseconds();
                var request = new PlayerRecentlyPlayedRequest
                {
                    Limit = 50, // Maximum allowed by Spotify
                    After = afterTimestamp
                };
                recentlyPlayed = await _spotifyClient.Client.Player.GetRecentlyPlayed(request);
            }
            else
            {
                var request = new PlayerRecentlyPlayedRequest
                {
                    Limit = 50 // Maximum allowed by Spotify
                };
                recentlyPlayed = await _spotifyClient.Client.Player.GetRecentlyPlayed(request);
            }

            if (recentlyPlayed?.Items == null || !recentlyPlayed.Items.Any())
            {
                _logger.LogInformation("No new recently played tracks");
                return;
            }

            _logger.LogInformation("Found {Count} recently played tracks", recentlyPlayed.Items.Count);

            // Convert to our PlayHistory entities
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

            // Save to database
            if (playHistories.Any())
            {
                await playHistoryService.SavePlayHistoryBatchAsync(playHistories);
                _logger.LogInformation("Successfully saved {Count} play history records", playHistories.Count);
            }
        }
        catch (APIException apiEx)
        {
            _logger.LogError(apiEx, "Spotify API error while fetching recently played: {Message}", apiEx.Message);

            // If rate limited, log the retry-after header
            if (apiEx.Response?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Rate limited by Spotify API. Will retry after next polling interval.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error polling recently played tracks");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Playback Tracking Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
