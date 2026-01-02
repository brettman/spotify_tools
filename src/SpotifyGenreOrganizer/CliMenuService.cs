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
        var title = new FigletText("Spotify Tools")
            .Centered()
            .Color(Color.Cyan1);

        AnsiConsole.Write(title);

        var subtitle = new Panel(new Markup(
            "[cyan]Sync your Spotify library to PostgreSQL for analytics[/]\n" +
            "[dim]Interactive CLI powered by Spectre.Console[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Padding(1, 0);

        AnsiConsole.Write(subtitle);
        AnsiConsole.WriteLine();
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
        AnsiConsole.Write(new Rule("[yellow bold]Partial Sync[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var choice = MenuBuilder.ShowPartialSyncMenu();

        if (choice == "Back to Main Menu")
        {
            return;
        }

        if (choice.Contains("disabled"))
        {
            AnsiConsole.MarkupLine("[red]❌ Audio Features sync is disabled (Spotify API deprecated).[/]");
            return;
        }

        // Map choice to sync action and stage name
        var (syncAction, stageName) = choice switch
        {
            "Tracks" => ((Func<Task<int>>)(() => _syncService.SyncTracksOnlyAsync()), "Tracks"),
            "Artists" => ((Func<Task<int>>)(() => _syncService.SyncArtistsOnlyAsync()), "Artists"),
            "Albums" => ((Func<Task<int>>)(() => _syncService.SyncAlbumsOnlyAsync()), "Albums"),
            "Playlists" => ((Func<Task<int>>)(() => _syncService.SyncPlaylistsOnlyAsync()), "Playlists"),
            _ => ((Func<Task<int>>?)null, "Unknown")!
        };

        if (syncAction == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Invalid choice.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"\n[cyan]Starting {stageName.EscapeMarkup()} sync...[/]");
        AnsiConsole.MarkupLine("[dim]This may take a while depending on your library size.[/]\n");

        var startTime = DateTime.Now;

        try
        {
            using var progressAdapter = new ProgressAdapter(_syncService);
            var count = await progressAdapter.RunWithProgressAsync(
                syncAction,
                $"Syncing {stageName}..."
            );

            var duration = DateTime.Now - startTime;

            AnsiConsole.MarkupLine($"\n[green]✓ {stageName.EscapeMarkup()} sync completed![/] Processed: {count}");
            AnsiConsole.MarkupLine($"[yellow]⏱  Duration:[/] {duration:hh\\:mm\\:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Stage} sync failed", stageName);
            AnsiConsole.MarkupLine($"\n[red]❌ Sync failed: {ex.Message.EscapeMarkup()}[/]");
        }
    }


    private async Task ViewLastSyncStatusAsync()
    {
        AnsiConsole.Write(new Rule("[cyan bold]Last Sync Status[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        var lastSyncDate = await _syncService.GetLastSyncDateAsync();

        if (lastSyncDate == null)
        {
            AnsiConsole.MarkupLine("[yellow]No sync has been completed yet.[/]");
            return;
        }

        var syncHistory = (await _unitOfWork.SyncHistory.GetAllAsync())
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (syncHistory == null)
        {
            AnsiConsole.MarkupLine("[yellow]No sync history found.[/]");
            return;
        }

        // Build status content
        var statusMarkup = GetStatusMarkup(syncHistory.Status);
        var duration = syncHistory.CompletedAt - syncHistory.StartedAt;

        var summaryContent = new Markup(
            $"[bold]Last Sync:[/] {syncHistory.CompletedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
            $"[bold]Status:[/] {statusMarkup}\n" +
            $"[bold]Type:[/] {syncHistory.SyncType}\n" +
            $"[bold]Duration:[/] {(duration.HasValue ? duration.Value.ToString(@"hh\:mm\:ss") : "[dim]N/A[/]")}"
        );

        var summaryPanel = new Panel(summaryContent)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Header("[cyan bold]📊 Summary[/]");

        AnsiConsole.Write(summaryPanel);
        AnsiConsole.WriteLine();

        // Statistics Table
        var statsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title("[green bold]📈 Statistics[/]")
            .AddColumn(new TableColumn("[cyan]Metric[/]").LeftAligned())
            .AddColumn(new TableColumn("[yellow]Count[/]").RightAligned());

        statsTable.AddRow("Tracks Added", syncHistory.TracksAdded.ToString());
        statsTable.AddRow("Tracks Updated", syncHistory.TracksUpdated.ToString());
        statsTable.AddRow("Artists Added", syncHistory.ArtistsAdded.ToString());
        statsTable.AddRow("Albums Added", syncHistory.AlbumsAdded.ToString());
        statsTable.AddRow("Playlists Synced", syncHistory.PlaylistsSynced.ToString());

        AnsiConsole.Write(statsTable);

        // Error Panel (if present)
        if (!string.IsNullOrEmpty(syncHistory.ErrorMessage))
        {
            AnsiConsole.WriteLine();
            var errorPanel = new Panel(new Markup($"[red]{syncHistory.ErrorMessage.EscapeMarkup()}[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red)
                .Header("[red bold]❌ Error[/]");

            AnsiConsole.Write(errorPanel);
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
                case "Browse by Genre":
                    await _navigationService.NavigateByGenreAsync();
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
        AnsiConsole.Write(new Rule("[magenta bold]Test Artist API (Debug)[/]").RuleStyle("magenta"));
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
                await Task.Delay(500);
            });

        AnsiConsole.WriteLine();

        // Prompt for artist ID or use default
        var artistId = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Enter artist Spotify ID[/] [dim](or press Enter for default)[/]:")
                .PromptStyle("green")
                .AllowEmpty()
                .DefaultValue("0OdUWJ0sBjDrqHygGUXeCF")
                .DefaultValueStyle("dim")
        );

        AnsiConsole.MarkupLine($"\n[cyan]🧪 Testing Artist API with ID:[/] [yellow]{artistId.EscapeMarkup()}[/]");
        AnsiConsole.WriteLine();

        try
        {
            var startTime = DateTime.Now;
            var artist = await _spotifyClient.Client.Artists.Get(artistId);
            var duration = DateTime.Now - startTime;

            // Success Panel
            var successContent = new Markup(
                $"[bold]Artist:[/] {artist.Name.EscapeMarkup()}\n" +
                $"[bold]Popularity:[/] {artist.Popularity}\n" +
                $"[bold]Followers:[/] {artist.Followers.Total:N0}\n" +
                $"[bold]Genres:[/] {string.Join(", ", artist.Genres).EscapeMarkup()}\n" +
                $"[bold]Response Time:[/] [green]{duration.TotalMilliseconds:F0}ms[/]"
            );

            var successPanel = new Panel(successContent)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green)
                .Header("[green bold]✓ SUCCESS[/]");

            AnsiConsole.Write(successPanel);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ API is working - no rate limit issues detected[/]");
        }
        catch (SpotifyAPI.Web.APITooManyRequestsException ex)
        {
            // Rate Limit Error Panel
            var retryAfter = "not provided";
            if (ex.Response?.Headers?.ContainsKey("Retry-After") == true)
            {
                retryAfter = ex.Response.Headers["Retry-After"];
            }

            var errorContent = $"[bold]Retry-After Header:[/] {retryAfter.EscapeMarkup()}\n\n";

            if (int.TryParse(retryAfter, out var retrySeconds))
            {
                var hours = retrySeconds / 3600.0;
                if (hours >= 1)
                {
                    errorContent += $"[yellow bold]⚠️  DAILY QUOTA LIMIT DETECTED![/]\n" +
                                   $"Spotify wants you to wait {hours:F1} hours ({retrySeconds:N0} seconds)\n\n" +
                                   $"[dim]This indicates you've hit a daily API quota limit, not just rate limiting.\n" +
                                   $"You'll need to wait until the quota resets (typically 24 hours).[/]";
                }
                else
                {
                    errorContent += $"Rate limit retry after: {retrySeconds} seconds";
                }
            }
            else
            {
                errorContent += $"Could not parse Retry-After value: {retryAfter.EscapeMarkup()}";
            }

            errorContent += $"\n\n[dim]Full error:[/]\n{ex.Message.EscapeMarkup()}";

            var errorPanel = new Panel(new Markup(errorContent))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red)
                .Header("[red bold]❌ RATE LIMIT ERROR (429)[/]");

            AnsiConsole.Write(errorPanel);
        }
        catch (Exception ex)
        {
            var errorPanel = new Panel(new Markup(
                $"[bold]Type:[/] {ex.GetType().Name.EscapeMarkup()}\n" +
                $"[bold]Message:[/] {ex.Message.EscapeMarkup()}"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red)
                .Header("[red bold]❌ ERROR[/]");

            AnsiConsole.Write(errorPanel);
        }
    }

    private string GetStatusMarkup(SpotifyTools.Domain.Enums.SyncStatus status)
    {
        return status switch
        {
            SpotifyTools.Domain.Enums.SyncStatus.Success => "[green]✓ Success[/]",
            SpotifyTools.Domain.Enums.SyncStatus.Failed => "[red]❌ Failed[/]",
            SpotifyTools.Domain.Enums.SyncStatus.InProgress => "[yellow]🔄 In Progress[/]",
            SpotifyTools.Domain.Enums.SyncStatus.Partial => "[yellow]⚠ Partial[/]",
            _ => "[dim]? Unknown[/]"
        };
    }
}
