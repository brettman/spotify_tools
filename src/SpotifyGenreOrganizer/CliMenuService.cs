using Microsoft.Extensions.Logging;
using SpotifyClientService;
using SpotifyTools.Sync;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Analytics;
using Spectre.Console;
using SpotifyGenreOrganizer.UI;

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
    private readonly NavigationService _navigationService;
    private readonly ILogger<CliMenuService> _logger;

    public CliMenuService(
        ISpotifyClientService spotifyClient,
        ISyncService syncService,
        IUnitOfWork unitOfWork,
        IAnalyticsService analyticsService,
        NavigationService navigationService,
        ILogger<CliMenuService> logger)
    {
        _spotifyClient = spotifyClient ?? throw new ArgumentNullException(nameof(spotifyClient));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync()
    {
        AnsiConsole.Clear();
        ShowWelcome();

        while (true)
        {
            var choice = MenuBuilder.ShowMainMenu();

            try
            {
                switch (choice)
                {
                    case "Full Sync (Import all data)":
                        await FullSyncAsync();
                        break;
                    case "Partial Sync (Select stages)":
                        await PartialSyncAsync();
                        break;
                    case "View Last Sync Status":
                        await ViewLastSyncStatusAsync();
                        break;
                    case "View Sync History":
                        await ViewSyncHistoryAsync();
                        break;
                    case "Track Detail Report":
                        await ShowTrackDetailAsync();
                        break;
                    case "Test Artist API (Debug)":
                        await TestArtistApiAsync();
                        break;
                    case "Exit":
                        AnsiConsole.MarkupLine("[green]Goodbye![/]");
                        return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing menu option");
                AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message.EscapeMarkup()}[/]");
            }

            if (choice != "Exit")
            {
                AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                Console.ReadKey(intercept: true);
                AnsiConsole.Clear();
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
        AnsiConsole.Write(new Rule("[green bold]Full Sync[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        // Authenticate first
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("🔐 Authenticating with Spotify...", async ctx =>
            {
                if (!_spotifyClient.IsAuthenticated)
                {
                    await _spotifyClient.AuthenticateAsync();
                    ctx.Status("✓ Authenticated");
                }
                else
                {
                    ctx.Status("✓ Already authenticated");
                }
                await Task.Delay(500); // Brief pause to show status
            });

        AnsiConsole.MarkupLine("\n[cyan]Starting full sync...[/]");
        AnsiConsole.MarkupLine("[dim]This may take a while depending on your library size.[/]");
        AnsiConsole.MarkupLine("[dim]Rate limited to 30 requests/minute to respect Spotify API limits.[/]\n");

        var startTime = DateTime.Now;

        try
        {
            using var progressAdapter = new ProgressAdapter(_syncService);
            var syncId = await progressAdapter.RunWithProgressAsync(
                async () => await _syncService.FullSyncAsync(),
                "Initializing full sync..."
            );

            var duration = DateTime.Now - startTime;

            AnsiConsole.MarkupLine($"\n[green]✓ Sync completed successfully![/] (ID: {syncId})");
            AnsiConsole.MarkupLine($"[yellow]⏱  Duration:[/] {duration:hh\\:mm\\:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed");
            AnsiConsole.MarkupLine($"\n[red]❌ Sync failed: {ex.Message.EscapeMarkup()}[/]");
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
        Console.WriteLine("  4. Playlists");
        Console.WriteLine("  5. Audio Features (disabled - API deprecated)");
        Console.WriteLine("  6. Back to main menu");
        Console.WriteLine();
        Console.Write("Select an option (1-6): ");

        var choice = Console.ReadLine()?.Trim();

        Func<Task<int>>? syncAction = choice switch
        {
            "1" => () => _syncService.SyncTracksOnlyAsync(),
            "2" => () => _syncService.SyncArtistsOnlyAsync(),
            "3" => () => _syncService.SyncAlbumsOnlyAsync(),
            "4" => () => _syncService.SyncPlaylistsOnlyAsync(),
            "5" => null, // Audio Features disabled
            "6" => null,
            _ => null
        };

        if (syncAction == null)
        {
            if (choice == "5")
                Console.WriteLine("\n❌ Audio Features sync is disabled (Spotify API deprecated).");
            else if (choice != "6")
                Console.WriteLine("\n❌ Invalid choice.");
            return;
        }

        var stageName = choice switch
        {
            "1" => "Tracks",
            "2" => "Artists",
            "3" => "Albums",
            "4" => "Playlists",
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
        AnsiConsole.WriteLine();

        var history = (await _unitOfWork.SyncHistory.GetAllAsync())
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .ToList();

        SpectreReportFormatter.RenderSyncHistoryTable(history);
    }

    private async Task ShowTrackDetailAsync()
    {
        var choice = MenuBuilder.ShowNavigationMenu();

        try
        {
            switch (choice)
            {
                case "Browse by Artist":
                    await _navigationService.NavigateByArtistAsync();
                    break;
                case "Browse by Playlist":
                    await _navigationService.NavigateByPlaylistAsync();
                    break;
                case "Search by Name":
                    await _navigationService.NavigateBySearchAsync();
                    break;
                case "Back to Main Menu":
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in navigation");
            AnsiConsole.MarkupLine($"\n[red]❌ Error: {ex.Message.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
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
