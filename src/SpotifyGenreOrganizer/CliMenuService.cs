using Microsoft.Extensions.Logging;
using SpotifyClientService;
using SpotifyTools.Sync;
using SpotifyTools.Data.Repositories.Interfaces;

namespace SpotifyGenreOrganizer;

/// <summary>
/// Interactive CLI menu service for Spotify Tools
/// </summary>
public class CliMenuService
{
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ISyncService _syncService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CliMenuService> _logger;

    public CliMenuService(
        ISpotifyClientService spotifyClient,
        ISyncService syncService,
        IUnitOfWork unitOfWork,
        ILogger<CliMenuService> logger)
    {
        _spotifyClient = spotifyClient ?? throw new ArgumentNullException(nameof(spotifyClient));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
                        await ViewLastSyncStatusAsync();
                        break;
                    case "3":
                        await ViewSyncHistoryAsync();
                        break;
                    case "4":
                        ShowAnalyticsPlaceholder();
                        break;
                    case "5":
                        Console.WriteLine("\nGoodbye!");
                        return;
                    default:
                        Console.WriteLine("\n❌ Invalid choice. Please select 1-5.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing menu option");
                Console.WriteLine($"\n❌ Error: {ex.Message}");
            }

            if (choice != "5")
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
        Console.WriteLine("│  2. View Last Sync Status              │");
        Console.WriteLine("│  3. View Sync History                  │");
        Console.WriteLine("│  4. Analytics (Coming soon)            │");
        Console.WriteLine("│  5. Exit                               │");
        Console.WriteLine("└────────────────────────────────────────┘");
        Console.Write("\nSelect an option (1-5): ");
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

    private void ShowAnalyticsPlaceholder()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║           Analytics                    ║");
        Console.WriteLine("╠════════════════════════════════════════╣");
        Console.WriteLine("║  Coming soon!                          ║");
        Console.WriteLine("║                                        ║");
        Console.WriteLine("║  • Tempo analysis                      ║");
        Console.WriteLine("║  • Key distribution                    ║");
        Console.WriteLine("║  • Genre statistics                    ║");
        Console.WriteLine("║  • Custom reports                      ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
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
