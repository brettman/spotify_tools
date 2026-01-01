using Microsoft.Extensions.Logging;
using SpotifyClientService;
using SpotifyTools.Sync;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Analytics;

namespace SpotifyGenreOrganizer;

/// <summary>
/// Interactive CLI menu service for Spotify Tools
/// </summary>
public class CliMenuService
{
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ISyncService _syncService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<CliMenuService> _logger;

    public CliMenuService(
        ISpotifyClientService spotifyClient,
        ISyncService syncService,
        IUnitOfWork unitOfWork,
        IAnalyticsService analyticsService,
        ILogger<CliMenuService> logger)
    {
        _spotifyClient = spotifyClient ?? throw new ArgumentNullException(nameof(spotifyClient));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync()
    {
        Console.Clear();
        ShowWelcome();

        while (true)
        {
            ShowMainMenu();
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        await FullSyncAsync();
                        break;
                    case "2":
                        await PartialSyncAsync();
                        break;
                    case "3":
                        await ViewLastSyncStatusAsync();
                        break;
                    case "4":
                        await ViewSyncHistoryAsync();
                        break;
                    case "5":
                        await ShowTrackDetailAsync();
                        break;
                    case "6":
                        await TestArtistApiAsync();
                        break;
                    case "7":
                        Console.WriteLine("\nGoodbye!");
                        return;
                    default:
                        Console.WriteLine("\n❌ Invalid choice. Please select 1-7.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing menu option");
                Console.WriteLine($"\n❌ Error: {ex.Message}");
            }

            if (choice != "7")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    private void ShowWelcome()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║     Spotify Tools - CLI Interface     ║");
        Console.WriteLine("╠════════════════════════════════════════╣");
        Console.WriteLine("║  Sync your Spotify library to          ║");
        Console.WriteLine("║  PostgreSQL for analytics              ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private void ShowMainMenu()
    {
        Console.WriteLine("\n┌────────────────────────────────────────┐");
        Console.WriteLine("│           Main Menu                    │");
        Console.WriteLine("├────────────────────────────────────────┤");
        Console.WriteLine("│  1. Full Sync (Import all data)       │");
        Console.WriteLine("│  2. Partial Sync (Select stages)      │");
        Console.WriteLine("│  3. View Last Sync Status              │");
        Console.WriteLine("│  4. View Sync History                  │");
        Console.WriteLine("│  5. Track Detail Report                │");
        Console.WriteLine("│  6. Test Artist API (Debug)            │");
        Console.WriteLine("│  7. Exit                               │");
        Console.WriteLine("└────────────────────────────────────────┘");
        Console.Write("\nSelect an option (1-7): ");
    }

    private async Task FullSyncAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║           Full Sync                    ║");
        Console.WriteLine("╚════════════════════════════════════════╝");

        // Authenticate first
        Console.WriteLine("\n🔐 Authenticating with Spotify...");
        if (!_spotifyClient.IsAuthenticated)
        {
            await _spotifyClient.AuthenticateAsync();
        }
        else
        {
            Console.WriteLine("✓ Already authenticated");
        }

        // Subscribe to progress events
        _syncService.ProgressChanged += OnSyncProgress;

        Console.WriteLine("\n🔄 Starting full sync...");
        Console.WriteLine("This may take a while depending on your library size.");
        Console.WriteLine("Rate limited to 60 requests/minute to respect Spotify API limits.\n");

        var startTime = DateTime.Now;

        try
        {
            var syncId = await _syncService.FullSyncAsync();
            var duration = DateTime.Now - startTime;

            Console.WriteLine($"\n✓ Sync completed successfully! (ID: {syncId})");
            Console.WriteLine($"⏱  Duration: {duration:hh\\:mm\\:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed");
            Console.WriteLine($"\n❌ Sync failed: {ex.Message}");
        }
        finally
        {
            _syncService.ProgressChanged -= OnSyncProgress;
        }
    }

    private async Task PartialSyncAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║         Partial Sync                   ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Select which stage to sync:");
        Console.WriteLine();
        Console.WriteLine("  1. Tracks");
        Console.WriteLine("  2. Artists");
        Console.WriteLine("  3. Albums");
        Console.WriteLine("  4. Audio Features");
        Console.WriteLine("  5. Playlists");
        Console.WriteLine("  6. Back to main menu");
        Console.WriteLine();
        Console.Write("Select an option (1-6): ");

        var choice = Console.ReadLine()?.Trim();

        Func<Task<int>>? syncAction = choice switch
        {
            "1" => () => _syncService.SyncTracksOnlyAsync(),
            "2" => () => _syncService.SyncArtistsOnlyAsync(),
            "3" => () => _syncService.SyncAlbumsOnlyAsync(),
            "4" => () => _syncService.SyncAudioFeaturesOnlyAsync(),
            "5" => () => _syncService.SyncPlaylistsOnlyAsync(),
            "6" => null,
            _ => null
        };

        if (syncAction == null)
        {
            if (choice != "6")
                Console.WriteLine("\n❌ Invalid choice.");
            return;
        }

        var stageName = choice switch
        {
            "1" => "Tracks",
            "2" => "Artists",
            "3" => "Albums",
            "4" => "Audio Features",
            "5" => "Playlists",
            _ => "Unknown"
        };

        Console.WriteLine($"\n🔄 Starting {stageName} sync...");
        Console.WriteLine();

        // Subscribe to progress events
        _syncService.ProgressChanged += OnSyncProgress;

        var startTime = DateTime.Now;

        try
        {
            var count = await syncAction();
            var duration = DateTime.Now - startTime;

            Console.WriteLine($"\n✓ {stageName} sync completed! Processed: {count}");
            Console.WriteLine($"⏱  Duration: {duration:hh\\:mm\\:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Stage} sync failed", stageName);
            Console.WriteLine($"\n❌ Sync failed: {ex.Message}");
        }
        finally
        {
            _syncService.ProgressChanged -= OnSyncProgress;
        }
    }

    private void OnSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        var percentage = e.Total > 0 ? (e.Current * 100 / e.Total) : 0;
        Console.WriteLine($"  [{e.Stage}] {e.Message} ({percentage}%)");
    }

    private async Task ViewLastSyncStatusAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║        Last Sync Status                ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        var lastSyncDate = await _syncService.GetLastSyncDateAsync();

        if (lastSyncDate == null)
        {
            Console.WriteLine("No sync has been completed yet.");
            return;
        }

        var syncHistory = (await _unitOfWork.SyncHistory.GetAllAsync())
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (syncHistory == null)
        {
            Console.WriteLine("No sync history found.");
            return;
        }

        Console.WriteLine($"Last Sync: {syncHistory.CompletedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Status: {GetStatusEmoji(syncHistory.Status)} {syncHistory.Status}");
        Console.WriteLine($"Type: {syncHistory.SyncType}");
        Console.WriteLine();
        Console.WriteLine("Statistics:");
        Console.WriteLine($"  • Tracks Added: {syncHistory.TracksAdded}");
        Console.WriteLine($"  • Tracks Updated: {syncHistory.TracksUpdated}");
        Console.WriteLine($"  • Artists Added: {syncHistory.ArtistsAdded}");
        Console.WriteLine($"  • Albums Added: {syncHistory.AlbumsAdded}");
        Console.WriteLine($"  • Playlists Synced: {syncHistory.PlaylistsSynced}");

        if (!string.IsNullOrEmpty(syncHistory.ErrorMessage))
        {
            Console.WriteLine($"\nError: {syncHistory.ErrorMessage}");
        }

        var duration = syncHistory.CompletedAt - syncHistory.StartedAt;
        if (duration.HasValue)
        {
            Console.WriteLine($"\nDuration: {duration.Value:hh\\:mm\\:ss}");
        }
    }

    private async Task ViewSyncHistoryAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║          Sync History                  ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        var history = (await _unitOfWork.SyncHistory.GetAllAsync())
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .ToList();

        if (!history.Any())
        {
            Console.WriteLine("No sync history found.");
            return;
        }

        Console.WriteLine("Last 10 syncs:\n");
        Console.WriteLine("┌──────┬─────────────────────┬──────────┬────────┬────────────┐");
        Console.WriteLine("│ ID   │ Date                │ Type     │ Status │ Tracks     │");
        Console.WriteLine("├──────┼─────────────────────┼──────────┼────────┼────────────┤");

        foreach (var sync in history)
        {
            var date = sync.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var type = sync.SyncType.ToString().PadRight(8);
            var status = $"{GetStatusEmoji(sync.Status)} {sync.Status}".PadRight(10);
            var tracks = $"{sync.TracksAdded}".PadLeft(10);

            Console.WriteLine($"│ {sync.Id,-4} │ {date,-19} │ {type} │ {status} │ {tracks} │");
        }

        Console.WriteLine("└──────┴─────────────────────┴──────────┴────────┴────────────┘");
    }

    private async Task ShowTrackDetailAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║        Track Detail Report             ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();

        // Prompt for track search
        Console.Write("Enter track name to search: ");
        var searchTerm = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            Console.WriteLine("\n❌ Search term cannot be empty.");
            return;
        }

        try
        {
            // Search for tracks
            var results = await _analyticsService.SearchTracksAsync(searchTerm, 10);

            if (!results.Any())
            {
                Console.WriteLine($"\n❌ No tracks found matching '{searchTerm}'");
                return;
            }

            // Display search results
            Console.WriteLine($"\nFound {results.Count} track(s):");
            Console.WriteLine();
            for (int i = 0; i < results.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {results[i].DisplayName}");
            }

            // Prompt for selection
            Console.WriteLine();
            Console.Write($"Select a track (1-{results.Count}) or 0 to cancel: ");
            var selectionInput = Console.ReadLine()?.Trim();

            if (!int.TryParse(selectionInput, out var selection) || selection < 0 || selection > results.Count)
            {
                Console.WriteLine("\n❌ Invalid selection.");
                return;
            }

            if (selection == 0)
            {
                Console.WriteLine("\nCancelled.");
                return;
            }

            // Get and display track detail report
            var trackId = results[selection - 1].TrackId;
            var report = await _analyticsService.GetTrackDetailReportAsync(trackId);

            if (report == null)
            {
                Console.WriteLine($"\n❌ Could not load details for selected track.");
                return;
            }

            // Format and display the report
            var formattedReport = ReportFormatter.FormatTrackDetailReport(report);
            Console.WriteLine(formattedReport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying track detail report");
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
    }

    private async Task TestArtistApiAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║      Test Artist API (Debug)           ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();

        // Authenticate first
        Console.WriteLine("🔐 Authenticating with Spotify...");
        if (!_spotifyClient.IsAuthenticated)
        {
            await _spotifyClient.AuthenticateAsync();
        }
        else
        {
            Console.WriteLine("✓ Already authenticated");
        }
        Console.WriteLine();

        // Prompt for artist ID or use default
        Console.WriteLine("Enter an artist Spotify ID to test");
        Console.WriteLine("(or press Enter to use default: 0OdUWJ0sBjDrqHygGUXeCF - Band of Horses)");
        Console.Write("> ");
        var artistId = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(artistId))
        {
            artistId = "0OdUWJ0sBjDrqHygGUXeCF"; // Band of Horses
        }

        Console.WriteLine();
        Console.WriteLine($"🧪 Testing Artist API with ID: {artistId}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        try
        {
            var startTime = DateTime.Now;
            var artist = await _spotifyClient.Client.Artists.Get(artistId);
            var duration = DateTime.Now - startTime;

            Console.WriteLine("✓ SUCCESS!");
            Console.WriteLine();
            Console.WriteLine($"Artist: {artist.Name}");
            Console.WriteLine($"Popularity: {artist.Popularity}");
            Console.WriteLine($"Followers: {artist.Followers.Total:N0}");
            Console.WriteLine($"Genres: {string.Join(", ", artist.Genres)}");
            Console.WriteLine($"Response Time: {duration.TotalMilliseconds:F0}ms");
            Console.WriteLine();
            Console.WriteLine("✓ API is working - no rate limit issues detected");
        }
        catch (SpotifyAPI.Web.APITooManyRequestsException ex)
        {
            Console.WriteLine("❌ RATE LIMIT ERROR (429)");
            Console.WriteLine();

            // Try to get Retry-After header
            var retryAfter = "not provided";
            if (ex.Response?.Headers?.ContainsKey("Retry-After") == true)
            {
                retryAfter = ex.Response.Headers["Retry-After"];
            }

            Console.WriteLine($"Retry-After Header: {retryAfter}");
            Console.WriteLine();

            if (int.TryParse(retryAfter, out var retrySeconds))
            {
                var hours = retrySeconds / 3600.0;
                if (hours >= 1)
                {
                    Console.WriteLine($"⚠️  DAILY QUOTA LIMIT DETECTED!");
                    Console.WriteLine($"   Spotify wants you to wait {hours:F1} hours ({retrySeconds:N0} seconds)");
                    Console.WriteLine();
                    Console.WriteLine("This indicates you've hit a daily API quota limit, not just rate limiting.");
                    Console.WriteLine("You'll need to wait until the quota resets (typically 24 hours).");
                }
                else
                {
                    Console.WriteLine($"Rate limit retry after: {retrySeconds} seconds");
                }
            }
            else
            {
                Console.WriteLine($"Could not parse Retry-After value: {retryAfter}");
            }

            Console.WriteLine();
            Console.WriteLine("Full error:");
            Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    private string GetStatusEmoji(SpotifyTools.Domain.Enums.SyncStatus status)
    {
        return status switch
        {
            SpotifyTools.Domain.Enums.SyncStatus.Success => "✓",
            SpotifyTools.Domain.Enums.SyncStatus.Failed => "❌",
            SpotifyTools.Domain.Enums.SyncStatus.InProgress => "🔄",
            SpotifyTools.Domain.Enums.SyncStatus.Partial => "⚠",
            _ => "?"
        };
    }
}
