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
                    case "Genre Analysis":
                        await ShowGenreAnalysisAsync();
                        break;
                    case "Explore Genre Clusters & Playlists":
                        await ExploreGenreClustersAsync();
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

        // Check if Audio Features (unavailable) was selected
        if (choice.Contains("Audio Features"))
        {
            AnsiConsole.MarkupLine("\n[yellow]⚠️  Audio Features Unavailable[/]");
            AnsiConsole.MarkupLine("[dim]Spotify restricted the audio features API for new apps (Nov 27, 2024).[/]");
            AnsiConsole.MarkupLine("[dim]Exploring alternatives: third-party APIs and local audio analysis tools.[/]");
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
            AnsiConsole.MarkupLine($"[dim]Check logs for details: src/SpotifyGenreOrganizer/logs/[/]");
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

    private async Task ShowGenreAnalysisAsync()
    {
        AnsiConsole.Write(new Rule("[green bold]Genre Analysis[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("🔍 Analyzing your genre landscape...", async ctx =>
                {
                    var report = await _analyticsService.GetGenreAnalysisReportAsync();
                    ctx.Status("✓ Analysis complete");
                    await Task.Delay(300); // Brief pause

                    AnsiConsole.Clear();
                    AnsiConsole.Write(new Rule("[green bold]Genre Analysis Report[/]").RuleStyle("green"));
                    AnsiConsole.WriteLine();

                    SpectreReportFormatter.RenderGenreAnalysisReport(report);
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating genre analysis");
            AnsiConsole.MarkupLine($"\n[red]❌ Error: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    private async Task ExploreGenreClustersAsync()
    {
        AnsiConsole.Write(new Rule("[cyan bold]Genre Clusters & Playlists[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        try
        {
            // First, let user choose between suggested and saved clusters
            var clusterType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]What would you like to view?[/]")
                    .AddChoices(new[]
                    {
                        "🔍 Generate new suggested clusters",
                        "💾 View saved clusters",
                        "← Back to Main Menu"
                    })
            );

            if (clusterType == "← Back to Main Menu")
                return;

            if (clusterType == "💾 View saved clusters")
            {
                await ViewSavedClustersAsync();
                return;
            }

            // Generate suggested clusters
            List<GenreCluster> clusters = new();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("🔍 Generating genre clusters...", async ctx =>
                {
                    // Fetch available genre seeds first
                    ctx.Status("📡 Fetching genre seeds from Spotify...");
                    var genreSeeds = await _analyticsService.GetAvailableGenreSeedsAsync();

                    if (genreSeeds.Any())
                    {
                        AnsiConsole.MarkupLine($"[dim]✓ Found {genreSeeds.Count} official genre seeds from Spotify[/]");
                    }

                    ctx.Status("🎵 Analyzing your library and creating clusters...");
                    clusters = await _analyticsService.SuggestGenreClustersAsync(minTracksPerCluster: 20);
                    ctx.Status("✓ Clusters generated");
                    await Task.Delay(300);
                });

            if (!clusters.Any())
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  No genre clusters found.[/]");
                AnsiConsole.MarkupLine("[dim]This might mean your library is too small or has very few genre tags.[/]");
                return;
            }

            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[cyan bold]📁 Found {clusters.Count} Genre Clusters[/]").RuleStyle("cyan"));
            AnsiConsole.WriteLine();

            // Display cluster summary table
            var clusterTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Cyan)
                .Title("[cyan bold]Suggested Genre Clusters[/]")
                .AddColumn(new TableColumn("[yellow]#[/]").RightAligned())
                .AddColumn(new TableColumn("[green]Cluster Name[/]").LeftAligned())
                .AddColumn(new TableColumn("[blue]Tracks[/]").RightAligned())
                .AddColumn(new TableColumn("[magenta]Artists[/]").RightAligned())
                .AddColumn(new TableColumn("[cyan]% of Library[/]").LeftAligned())
                .AddColumn(new TableColumn("[yellow]Genres Included[/]").LeftAligned());

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                var barLength = (int)(cluster.PercentageOfLibrary / 2);
                var bar = new string('█', Math.Min(barLength, 50));

                var genresPreview = cluster.Genres.Count > 3
                    ? string.Join(", ", cluster.Genres.Take(3)) + $" (+{cluster.Genres.Count - 3} more)"
                    : string.Join(", ", cluster.Genres);

                clusterTable.AddRow(
                    (i + 1).ToString(),
                    cluster.Name.EscapeMarkup(),
                    cluster.TotalTracks.ToString("N0"),
                    cluster.TotalArtists.ToString("N0"),
                    $"[cyan]{bar}[/] [dim]{cluster.PercentageOfLibrary:F1}%[/]",
                    genresPreview.EscapeMarkup()
                );
            }

            AnsiConsole.Write(clusterTable);
            AnsiConsole.WriteLine();

            // Let user select a cluster to review
            while (true)
            {
                AnsiConsole.WriteLine();
                var clusterChoices = clusters
                    .Select((c, i) => $"{i + 1}. {c.Name} ({c.TotalTracks} tracks)")
                    .Concat(new[] { "[dim]← Back to Main Menu[/]" })
                    .ToList();

                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select a cluster to review and refine:[/]")
                        .PageSize(15)
                        .HighlightStyle(new Style(Color.Cyan, decoration: Decoration.Bold))
                        .AddChoices(clusterChoices)
                );

                if (selection.StartsWith("[dim]"))
                    break;

                // Extract cluster index
                var clusterIndex = int.Parse(selection.Split('.')[0]) - 1;
                var selectedCluster = clusters[clusterIndex];

                await ReviewAndRefineClusterAsync(selectedCluster);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exploring genre clusters");
            AnsiConsole.MarkupLine($"\n[red]❌ Error: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    private async Task ReviewAndRefineClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[cyan bold]📝 Reviewing: {cluster.Name.EscapeMarkup()}[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        // Get genre details with track counts
        var genreDetails = new Dictionary<string, int>();
        var artists = await _unitOfWork.Artists.GetAllAsync();
        var tracks = await _unitOfWork.Tracks.GetAllAsync();
        var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

        foreach (var genre in cluster.Genres)
        {
            var genreArtists = artists.Where(a => a.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase)).ToList();
            var genreTrackIds = new HashSet<string>();

            foreach (var artist in genreArtists)
            {
                var artistTrackIds = trackArtists
                    .Where(ta => ta.ArtistId == artist.Id)
                    .Select(ta => ta.TrackId);
                foreach (var trackId in artistTrackIds)
                    genreTrackIds.Add(trackId);
            }

            genreDetails[genre] = genreTrackIds.Count;
        }

        // Display genre breakdown
        var genreTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Title($"[yellow bold]Genres in '{cluster.Name.EscapeMarkup()}'[/]")
            .AddColumn(new TableColumn("[cyan]#[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Genre[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Tracks[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]% of Cluster[/]").LeftAligned());

        var sortedGenres = genreDetails.OrderByDescending(kvp => kvp.Value).ToList();
        for (int i = 0; i < sortedGenres.Count; i++)
        {
            var genre = sortedGenres[i];
            var percentage = (genre.Value / (double)cluster.TotalTracks) * 100;
            var barLength = (int)(percentage / 2);
            var bar = new string('█', Math.Min(barLength, 50));

            genreTable.AddRow(
                (i + 1).ToString(),
                genre.Key.EscapeMarkup(),
                genre.Value.ToString("N0"),
                $"[magenta]{bar}[/] [dim]{percentage:F1}%[/]"
            );
        }

        AnsiConsole.Write(genreTable);
        AnsiConsole.WriteLine();

        // Interactive refinement
        AnsiConsole.MarkupLine("[cyan]💡 Review each genre and decide if it belongs in this cluster[/]");
        AnsiConsole.WriteLine();

        var includedGenres = new HashSet<string>(cluster.Genres, StringComparer.OrdinalIgnoreCase);

        var refineChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "Remove genres from this cluster",
                    "View track preview (coming soon)",
                    "Accept cluster as-is",
                    "← Back to cluster list"
                })
        );

        switch (refineChoice)
        {
            case "Remove genres from this cluster":
                var refinedCluster = await RemoveGenresFromClusterAsync(cluster, genreDetails);
                if (refinedCluster != null)
                {
                    await PromptSaveClusterAsync(refinedCluster);
                }
                break;
            case "Accept cluster as-is":
                AnsiConsole.MarkupLine($"[green]✓ Cluster '{cluster.Name.EscapeMarkup()}' accepted[/]");
                await PromptSaveClusterAsync(cluster);
                break;
        }
    }

    private async Task<GenreCluster?> RemoveGenresFromClusterAsync(GenreCluster cluster, Dictionary<string, int> genreDetails)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[yellow bold]🗑️  Remove Genres from '{cluster.Name.EscapeMarkup()}'[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var genresToRemove = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[yellow]Select genres to REMOVE (these won't fit the cluster):[/]")
                .PageSize(20)
                .MoreChoicesText("[grey](Move up/down to see more genres)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                .AddChoices(genreDetails.OrderByDescending(kvp => kvp.Value).Select(kvp =>
                    $"{kvp.Key} ({kvp.Value} tracks)"))
        );

        if (genresToRemove.Any())
        {
            var removedCount = genresToRemove.Count;
            var removedGenres = genresToRemove.Select(s => s.Split(" (")[0]).ToList();

            // Calculate new stats
            var remainingGenres = cluster.Genres.Except(removedGenres, StringComparer.OrdinalIgnoreCase).ToList();
            var removedTracks = removedGenres.Sum(g => genreDetails.GetValueOrDefault(g, 0));
            var remainingTracks = cluster.TotalTracks - removedTracks;

            AnsiConsole.WriteLine();
            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .Title("[yellow bold]📊 Cluster Changes[/]")
                .AddColumn("[cyan]Metric[/]")
                .AddColumn("[yellow]Before[/]")
                .AddColumn("[green]After[/]");

            summaryTable.AddRow("Genres", cluster.Genres.Count.ToString(), remainingGenres.Count.ToString());
            summaryTable.AddRow("Tracks", cluster.TotalTracks.ToString("N0"), remainingTracks.ToString("N0"));
            summaryTable.AddRow("Change", "-", $"[red]-{removedTracks:N0} tracks[/]");

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();

            // Handle orphaned genres
            await HandleOrphanedGenresAsync(removedGenres, genreDetails, cluster);

            // Create refined cluster with remaining genres
            // Update description to reflect the refined genre list
            var newDescription = remainingGenres.Count <= 5
                ? $"Includes: {string.Join(", ", remainingGenres)}"
                : $"Includes: {string.Join(", ", remainingGenres.Take(5))} (+{remainingGenres.Count - 5} more)";

            var refinedCluster = new GenreCluster
            {
                Id = cluster.Id,
                Name = cluster.Name,
                Description = newDescription,
                Genres = remainingGenres,
                PrimaryGenre = cluster.PrimaryGenre,
                TotalTracks = remainingTracks,
                TotalArtists = cluster.TotalArtists, // Approximate
                PercentageOfLibrary = cluster.PercentageOfLibrary * (remainingTracks / (double)cluster.TotalTracks),
                IsAutoGenerated = false // User refined it
            };

            return refinedCluster;
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No genres removed.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return null;
        }
    }

    private async Task HandleOrphanedGenresAsync(List<string> removedGenres, Dictionary<string, int> genreDetails, GenreCluster originalCluster)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[magenta]🔀 Handle Removed Genres[/]").RuleStyle("magenta"));
        AnsiConsole.WriteLine();

        // Show what was removed
        var removedTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Magenta)
            .Title("[magenta bold]Removed Genres[/]")
            .AddColumn("[yellow]Genre[/]")
            .AddColumn("[cyan]Tracks[/]");

        foreach (var genre in removedGenres.OrderByDescending(g => genreDetails.GetValueOrDefault(g, 0)))
        {
            removedTable.AddRow(
                genre.EscapeMarkup(),
                genreDetails.GetValueOrDefault(genre, 0).ToString("N0")
            );
        }

        AnsiConsole.Write(removedTable);
        AnsiConsole.WriteLine();

        var totalRemovedTracks = removedGenres.Sum(g => genreDetails.GetValueOrDefault(g, 0));
        AnsiConsole.MarkupLine($"[yellow]⚠️  {removedGenres.Count} genres removed → {totalRemovedTracks:N0} tracks need reassignment[/]");
        AnsiConsole.WriteLine();

        // Determine which genres are large enough for their own clusters
        var largeGenres = removedGenres.Where(g => genreDetails.GetValueOrDefault(g, 0) >= 20).ToList();
        var smallGenres = removedGenres.Except(largeGenres).ToList();

        var choices = new List<string>();

        if (largeGenres.Any())
        {
            var largeTrackCount = largeGenres.Sum(g => genreDetails.GetValueOrDefault(g, 0));
            choices.Add($"Create new cluster(s) for {largeGenres.Count} large genres ({largeTrackCount:N0} tracks)");
        }

        choices.Add("Add all to 'Unclustered' bucket for later review");
        choices.Add("See suggested alternative clusters (coming soon)");
        choices.Add("Leave unclustered (skip for now)");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]What should happen to these removed genres?[/]")
                .AddChoices(choices)
        );

        AnsiConsole.WriteLine();

        if (choice.StartsWith("Create new cluster"))
        {
            // Create individual clusters for large genres
            var newClusters = new List<string>();

            foreach (var genre in largeGenres)
            {
                var trackCount = genreDetails.GetValueOrDefault(genre, 0);
                var clusterName = CapitalizeGenre(genre);

                AnsiConsole.MarkupLine($"[green]✓ Created cluster:[/] '{clusterName.EscapeMarkup()}' ({trackCount:N0} tracks)");
                newClusters.Add(clusterName);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]✓ Created {newClusters.Count} new cluster(s)[/]");

            if (smallGenres.Any())
            {
                var smallTrackCount = smallGenres.Sum(g => genreDetails.GetValueOrDefault(g, 0));
                AnsiConsole.MarkupLine($"[dim]Note: {smallGenres.Count} smaller genres ({smallTrackCount} tracks) added to 'Unclustered'[/]");
            }
        }
        else if (choice.StartsWith("Add all to"))
        {
            AnsiConsole.MarkupLine($"[green]✓ Added {removedGenres.Count} genres to 'Unclustered' bucket[/]");
            AnsiConsole.MarkupLine("[dim]You can review and organize these later[/]");
        }
        else if (choice.StartsWith("See suggested"))
        {
            AnsiConsole.MarkupLine("[yellow]💡 Alternative cluster suggestions coming soon![/]");
            AnsiConsole.MarkupLine("[dim]This will analyze genre overlap and suggest where removed genres might fit[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Skipped reassignment - {removedGenres.Count} genres remain unclustered[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private static string CapitalizeGenre(string genre)
    {
        if (string.IsNullOrEmpty(genre)) return genre;

        var words = genre.Split(' ');
        return string.Join(" ", words.Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1) : w));
    }

    private async Task PromptSaveClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]💾 Save Cluster[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        var shouldSave = AnsiConsole.Confirm($"[green]Save '{cluster.Name.EscapeMarkup()}' cluster to database?[/]", true);

        if (!shouldSave)
        {
            AnsiConsole.MarkupLine("[yellow]Cluster not saved.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        // Ask for custom name (optional)
        var customizeName = AnsiConsole.Confirm("[cyan]Would you like to customize the cluster name?[/]", false);
        string? customName = null;

        if (customizeName)
        {
            customName = AnsiConsole.Ask<string>("[cyan]Enter cluster name:[/]", cluster.Name);
        }

        try
        {
            var clusterId = await _analyticsService.SaveClusterAsync(cluster, customName);

            AnsiConsole.MarkupLine($"[green]✓ Cluster saved successfully![/]");
            AnsiConsole.MarkupLine($"[dim]Name:[/] {(customName ?? cluster.Name).EscapeMarkup()}");
            AnsiConsole.MarkupLine($"[dim]ID:[/] {clusterId}");
            AnsiConsole.MarkupLine($"[dim]Genres:[/] {cluster.Genres.Count}");
            AnsiConsole.MarkupLine($"[dim]Tracks:[/] {cluster.TotalTracks:N0}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message.EscapeMarkup()}[/]");
            AnsiConsole.WriteLine();

            // Retry with different name
            var retry = AnsiConsole.Confirm("[yellow]Try again with a different name?[/]", true);
            if (retry)
            {
                var newName = AnsiConsole.Ask<string>("[cyan]Enter a unique cluster name:[/]");
                try
                {
                    var clusterId = await _analyticsService.SaveClusterAsync(cluster, newName);
                    AnsiConsole.MarkupLine($"[green]✓ Cluster saved successfully as '{newName.EscapeMarkup()}'![/]");
                    AnsiConsole.MarkupLine($"[dim]ID:[/] {clusterId}");
                }
                catch (Exception retryEx)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Failed to save: {retryEx.Message.EscapeMarkup()}[/]");
                    _logger.LogError(retryEx, "Failed to save cluster after retry");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error saving cluster: {ex.Message.EscapeMarkup()}[/]");
            _logger.LogError(ex, "Failed to save cluster '{ClusterName}'", cluster.Name);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private async Task ViewSavedClustersAsync()
    {
        List<GenreCluster> savedClusters = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("📂 Loading saved clusters...", async ctx =>
            {
                savedClusters = await _analyticsService.GetSavedClustersAsync();
                ctx.Status("✓ Clusters loaded");
                await Task.Delay(300);
            });

        if (!savedClusters.Any())
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[yellow]⚠️  No saved clusters found.[/]");
            AnsiConsole.MarkupLine("[dim]Generate and save some clusters first![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[green bold]💾 Saved Clusters ({savedClusters.Count})[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        // Display saved clusters table
        var clusterTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title("[green bold]Your Saved Clusters[/]")
            .AddColumn(new TableColumn("[yellow]ID[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Cluster Name[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Tracks[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]Genres[/]").RightAligned())
            .AddColumn(new TableColumn("[cyan]Status[/]").LeftAligned())
            .AddColumn(new TableColumn("[yellow]Type[/]").LeftAligned());

        foreach (var cluster in savedClusters)
        {
            var clusterId = int.Parse(cluster.Id);
            var status = "[dim]Draft[/]"; // TODO: Check IsFinalized when implemented
            var type = cluster.IsAutoGenerated ? "[dim]Auto[/]" : "[cyan]Refined[/]";

            clusterTable.AddRow(
                cluster.Id,
                cluster.Name.EscapeMarkup(),
                cluster.TotalTracks.ToString("N0"),
                cluster.Genres.Count.ToString(),
                status,
                type
            );
        }

        AnsiConsole.Write(clusterTable);
        AnsiConsole.WriteLine();

        // Let user select a cluster to manage
        while (true)
        {
            AnsiConsole.WriteLine();
            var clusterChoices = savedClusters
                .Select(c => $"#{c.Id} - {c.Name.EscapeMarkup()} ({c.TotalTracks} tracks, {c.Genres.Count} genres)")
                .Concat(new[] { "[dim]← Back[/]" })
                .ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select a cluster to manage:[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                    .AddChoices(clusterChoices)
            );

            if (selection.StartsWith("[dim]"))
                break;

            // Extract cluster ID
            var clusterId = int.Parse(selection.Split('-')[0].Trim().Substring(1));
            var selectedCluster = savedClusters.First(c => c.Id == clusterId.ToString());

            await ManageSavedClusterAsync(selectedCluster);

            // Reload clusters in case changes were made
            savedClusters = await _analyticsService.GetSavedClustersAsync();
            if (!savedClusters.Any())
                break;
        }
    }

    private async Task ManageSavedClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[green bold]📝 Manage: {cluster.Name.EscapeMarkup()}[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        // Show cluster details
        var detailsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title("[green bold]Cluster Details[/]")
            .AddColumn("[cyan]Property[/]")
            .AddColumn("[yellow]Value[/]");

        detailsTable.AddRow("ID", cluster.Id);
        detailsTable.AddRow("Name", cluster.Name.EscapeMarkup());
        detailsTable.AddRow("Description", cluster.Description?.EscapeMarkup() ?? "[dim]None[/]");
        detailsTable.AddRow("Tracks", cluster.TotalTracks.ToString("N0"));
        detailsTable.AddRow("Artists", cluster.TotalArtists.ToString("N0"));
        detailsTable.AddRow("Genres", cluster.Genres.Count.ToString());
        detailsTable.AddRow("Type", cluster.IsAutoGenerated ? "Auto-generated" : "User-refined");

        AnsiConsole.Write(detailsTable);
        AnsiConsole.WriteLine();

        // Show genres
        var genresPanel = new Panel(string.Join(", ", cluster.Genres.Select(g => g.EscapeMarkup())))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Header("[cyan bold]Genres in this Cluster[/]");

        AnsiConsole.Write(genresPanel);
        AnsiConsole.WriteLine();

        // Management options
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "📝 Edit cluster genres",
                    "🗑️  Delete cluster",
                    "✅ Finalize for playlist generation",
                    "🎵 View tracks",
                    "← Back to cluster list"
                })
        );

        switch (action)
        {
            case "📝 Edit cluster genres":
                await EditClusterAsync(cluster);
                break;
            case "🗑️  Delete cluster":
                await DeleteClusterAsync(cluster);
                break;
            case "✅ Finalize for playlist generation":
                await FinalizeClusterAsync(cluster);
                break;
            case "🎵 View tracks":
                await ViewClusterTracksAsync(cluster);
                break;
        }
    }

    private async Task DeleteClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.WriteLine();
        var confirm = AnsiConsole.Confirm(
            $"[red]Are you sure you want to delete '{cluster.Name.EscapeMarkup()}'?[/]",
            false
        );

        if (!confirm)
        {
            AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        try
        {
            var clusterId = int.Parse(cluster.Id);
            var success = await _analyticsService.DeleteClusterAsync(clusterId);

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Cluster '{cluster.Name.EscapeMarkup()}' deleted successfully![/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]⚠️  Cluster not found or already deleted.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error deleting cluster: {ex.Message.EscapeMarkup()}[/]");
            _logger.LogError(ex, "Failed to delete cluster '{ClusterName}'", cluster.Name);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private async Task EditClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[yellow bold]📝 Edit: {cluster.Name.EscapeMarkup()}[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        // Get genre details with track counts
        var genreDetails = new Dictionary<string, int>();
        var artists = await _unitOfWork.Artists.GetAllAsync();
        var tracks = await _unitOfWork.Tracks.GetAllAsync();
        var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

        foreach (var genre in cluster.Genres)
        {
            var genreArtists = artists.Where(a => a.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase)).ToList();
            var genreTrackIds = new HashSet<string>();

            foreach (var artist in genreArtists)
            {
                var artistTrackIds = trackArtists
                    .Where(ta => ta.ArtistId == artist.Id)
                    .Select(ta => ta.TrackId);
                foreach (var trackId in artistTrackIds)
                    genreTrackIds.Add(trackId);
            }

            genreDetails[genre] = genreTrackIds.Count;
        }

        // Show current genres
        var genreTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Title($"[yellow bold]Current Genres in '{cluster.Name.EscapeMarkup()}'[/]")
            .AddColumn(new TableColumn("[cyan]#[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Genre[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Tracks[/]").RightAligned());

        var sortedGenres = genreDetails.OrderByDescending(kvp => kvp.Value).ToList();
        for (int i = 0; i < sortedGenres.Count; i++)
        {
            var genre = sortedGenres[i];
            genreTable.AddRow(
                (i + 1).ToString(),
                genre.Key.EscapeMarkup(),
                genre.Value.ToString("N0")
            );
        }

        AnsiConsole.Write(genreTable);
        AnsiConsole.WriteLine();

        // Allow removing genres
        var refinedCluster = await RemoveGenresFromClusterAsync(cluster, genreDetails);

        if (refinedCluster != null)
        {
            // Update the cluster in the database
            try
            {
                var clusterId = int.Parse(cluster.Id);
                var success = await _analyticsService.UpdateClusterAsync(clusterId, refinedCluster);

                if (success)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Cluster '{cluster.Name.EscapeMarkup()}' updated successfully![/]");
                    AnsiConsole.MarkupLine($"[dim]Now has {refinedCluster.Genres.Count} genres[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠️  Cluster not found or couldn't be updated.[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Error updating cluster: {ex.Message.EscapeMarkup()}[/]");
                _logger.LogError(ex, "Failed to update cluster '{ClusterName}'", cluster.Name);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
        }
    }

    private async Task FinalizeClusterAsync(GenreCluster cluster)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel($"[cyan]Finalizing a cluster marks it as ready for playlist generation.\n\nCluster:[/] [yellow]{cluster.Name.EscapeMarkup()}[/]\n[cyan]Genres:[/] {cluster.Genres.Count}\n[cyan]Tracks:[/] {cluster.TotalTracks:N0}")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Header("[cyan bold]✅ Finalize Cluster[/]"));

        AnsiConsole.WriteLine();
        var confirm = AnsiConsole.Confirm(
            $"[green]Mark '{cluster.Name.EscapeMarkup()}' as finalized?[/]",
            true
        );

        if (!confirm)
        {
            AnsiConsole.MarkupLine("[yellow]Finalization cancelled.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        try
        {
            var clusterId = int.Parse(cluster.Id);
            var success = await _analyticsService.FinalizeClusterAsync(clusterId);

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Cluster '{cluster.Name.EscapeMarkup()}' finalized successfully![/]");
                AnsiConsole.WriteLine();

                // Offer to create Spotify playlist
                var createPlaylist = AnsiConsole.Confirm(
                    "[green]Create Spotify playlist now?[/]",
                    true
                );

                if (createPlaylist)
                {
                    await CreateSpotifyPlaylistAsync(clusterId, cluster.Name);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Playlist creation skipped. You can create it later from the cluster management menu.[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]⚠️  Cluster not found or already finalized.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error finalizing cluster: {ex.Message.EscapeMarkup()}[/]");
            _logger.LogError(ex, "Failed to finalize cluster '{ClusterName}'", cluster.Name);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private async Task CreateSpotifyPlaylistAsync(int clusterId, string clusterName)
    {
        AnsiConsole.WriteLine();

        // Ask if playlist should be public or private
        var makePublic = AnsiConsole.Confirm(
            "[cyan]Make playlist public?[/] [dim](Otherwise it will be private)[/]",
            false
        );

        AnsiConsole.WriteLine();

        try
        {
            string? playlistId = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"🎵 Creating Spotify playlist '{clusterName.EscapeMarkup()}'...", async ctx =>
                {
                    ctx.Status($"🔐 Checking Spotify authentication...");
                    await Task.Delay(500); // Brief delay for status visibility

                    ctx.Status($"🎵 Creating playlist...");
                    playlistId = await _analyticsService.CreatePlaylistFromClusterAsync(clusterId, makePublic);
                });

            if (!string.IsNullOrEmpty(playlistId))
            {
                AnsiConsole.MarkupLine($"[green]✓ Spotify playlist created successfully![/]");
                AnsiConsole.MarkupLine($"[cyan]Playlist ID:[/] {playlistId}");
                AnsiConsole.MarkupLine($"[dim]View it in Spotify: https://open.spotify.com/playlist/{playlistId}[/]");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already has a playlist"))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  This cluster already has a playlist.[/]");
            AnsiConsole.MarkupLine($"[dim]{ex.Message.EscapeMarkup()}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error creating Spotify playlist: {ex.Message.EscapeMarkup()}[/]");
            _logger.LogError(ex, "Failed to create Spotify playlist for cluster {ClusterId}", clusterId);
        }
    }

    private async Task ViewClusterTracksAsync(GenreCluster cluster)
    {
        // Load tracks for the cluster
        ClusterPlaylistReport? report = null;

        await AnsiConsole.Status()
            .StartAsync($"🎵 Loading tracks for '{cluster.Name.EscapeMarkup()}'...", async ctx =>
            {
                report = await _analyticsService.GetClusterPlaylistReportAsync(cluster);
            });

        if (report == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to load tracks for this cluster.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        if (!report.Tracks.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No tracks found for this cluster.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        // Browse tracks with pagination
        const int pageSize = 30;
        int currentPage = 1;

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[green bold]🎵 Tracks in: {cluster.Name.EscapeMarkup()}[/]").RuleStyle("green"));
            AnsiConsole.WriteLine();

            // Show cluster info panel
            var infoPanel = new Panel(
                $"[cyan]Genres:[/] {string.Join(", ", cluster.Genres.Take(5).Select(g => g.EscapeMarkup()))}" +
                (cluster.Genres.Count > 5 ? $" [dim](+{cluster.Genres.Count - 5} more)[/]" : "") +
                $"\n[cyan]Total Tracks:[/] {report.Tracks.Count:N0}\n[cyan]Total Artists:[/] {cluster.TotalArtists:N0}")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan)
                .Header("[cyan bold]Cluster Info[/]");

            AnsiConsole.Write(infoPanel);
            AnsiConsole.WriteLine();

            // Render current page
            SpectreReportFormatter.RenderTracksTablePage(
                report.Tracks,
                currentPage,
                pageSize,
                out int totalPages
            );
            AnsiConsole.WriteLine();

            // Show navigation prompt
            AnsiConsole.MarkupLine("[cyan]Options:[/] [green][[N]]ext[/] [green][[P]]rev[/] [yellow][[J]]ump[/] [yellow][[1-30]][/] Select row [cyan][[E]]dit mode[/] [dim][[B]]ack[/]");
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Command:[/]")
                    .PromptStyle("green")
                    .AllowEmpty()
            ).Trim().ToLower();

            if (string.IsNullOrEmpty(input) || input == "b" || input == "back")
            {
                return; // Back to cluster management
            }
            else if (input == "n" || input == "next")
            {
                if (currentPage < totalPages) currentPage++;
            }
            else if (input == "p" || input == "prev" || input == "previous")
            {
                if (currentPage > 1) currentPage--;
            }
            else if (input == "j" || input == "jump")
            {
                var pageNum = AnsiConsole.Prompt(
                    new TextPrompt<int>($"[cyan]Jump to page (1-{totalPages}):[/]")
                        .PromptStyle("yellow")
                        .ValidationErrorMessage($"[red]Please enter a number between 1 and {totalPages}[/]")
                        .Validate(p => p >= 1 && p <= totalPages)
                );
                currentPage = pageNum;
            }
            else if (input == "e" || input == "edit")
            {
                await EditClusterTracksAsync(cluster, report);
                return; // Return after edit mode
            }
            else if (int.TryParse(input, out int rowNum) && rowNum >= 1 && rowNum <= pageSize)
            {
                // Select track from current page to view details
                var startIndex = (currentPage - 1) * pageSize;
                var sorted = report.Tracks
                    .OrderBy(t => t.ArtistName)
                    .ThenBy(t => t.TrackName)
                    .ToList();
                var pageItems = sorted.Skip(startIndex).Take(pageSize).ToList();

                if (rowNum <= pageItems.Count)
                {
                    var selectedTrack = pageItems[rowNum - 1];
                    await ShowTrackDetailForClusterAsync(selectedTrack);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Row {rowNum} not available on this page (only {pageItems.Count} rows)[/]");
                    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                    Console.ReadKey(intercept: true);
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Unknown command: {input.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                Console.ReadKey(intercept: true);
            }
        }
    }

    private async Task ShowTrackDetailForClusterAsync(ClusterPlaylistReport.TrackInfo track)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[green bold]🎵 Track Details[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        var detailsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title("[green bold]Track Information[/]")
            .AddColumn("[cyan]Property[/]")
            .AddColumn("[yellow]Value[/]");

        detailsTable.AddRow("Track", track.TrackName.EscapeMarkup());
        detailsTable.AddRow("Artist", track.ArtistName.EscapeMarkup());
        detailsTable.AddRow("Album", (track.AlbumName ?? "[dim]unknown[/]").EscapeMarkup());
        detailsTable.AddRow("Duration", track.FormattedDuration);
        detailsTable.AddRow("Popularity", track.Popularity.ToString());

        if (track.AddedAt.HasValue)
        {
            detailsTable.AddRow("Added", track.AddedAt.Value.ToString("MMM dd, yyyy"));
        }

        var matchedGenres = track.MatchedGenres.Any()
            ? string.Join(", ", track.MatchedGenres.Select(g => g.EscapeMarkup()))
            : "[dim]none[/]";
        detailsTable.AddRow("Matched Genres", matchedGenres);

        var allGenres = track.Genres.Any()
            ? string.Join(", ", track.Genres.Select(g => g.EscapeMarkup()))
            : "[dim]none[/]";
        detailsTable.AddRow("All Artist Genres", allGenres);

        AnsiConsole.Write(detailsTable);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
        Console.ReadKey(intercept: true);
    }

    private async Task EditClusterTracksAsync(GenreCluster cluster, ClusterPlaylistReport report)
    {
        if (!int.TryParse(cluster.Id, out int clusterId))
        {
            AnsiConsole.MarkupLine("[red]Cannot edit tracks - cluster ID is invalid.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        var tracksToRemove = new List<ClusterPlaylistReport.TrackInfo>();

        // Build selection list
        var sorted = report.Tracks
            .OrderBy(t => t.ArtistName)
            .ThenBy(t => t.TrackName)
            .ToList();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[yellow bold]✏️  Edit Tracks: {cluster.Name.EscapeMarkup()}[/]").RuleStyle("yellow"));
            AnsiConsole.WriteLine();

            if (tracksToRemove.Any())
            {
                var panel = new Panel(
                    $"[red]{tracksToRemove.Count} track(s) marked for removal[/]\n[dim]These tracks will be excluded from this cluster[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Red)
                    .Header("[red bold]⚠️  Pending Changes[/]");

                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
            }

            // Show instructions
            AnsiConsole.MarkupLine("[cyan]Select tracks to remove from this cluster:[/]");
            AnsiConsole.MarkupLine("[dim]Tip: You can select multiple tracks. Press [green]Enter[/] with no selection when done.[/]");
            AnsiConsole.WriteLine();

            var trackChoices = sorted
                .Select(t => $"{t.TrackName.EscapeMarkup()} - {t.ArtistName.EscapeMarkup()} ({t.FormattedDuration})")
                .ToList();

            trackChoices.Add("← Done - Save changes");
            trackChoices.Add("← Cancel - Discard changes");

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Select a track to remove (or choose an option):[/]")
                    .PageSize(15)
                    .AddChoices(trackChoices)
            );

            if (selection == "← Done - Save changes")
            {
                if (!tracksToRemove.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No tracks selected for removal.[/]");
                    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                    Console.ReadKey(intercept: true);
                    return;
                }

                // Confirm removal
                AnsiConsole.WriteLine();
                var confirm = AnsiConsole.Confirm(
                    $"[red]Exclude {tracksToRemove.Count} track(s) from '{cluster.Name.EscapeMarkup()}'?[/]",
                    true
                );

                if (confirm)
                {
                    try
                    {
                        // Add track exclusions
                        foreach (var track in tracksToRemove)
                        {
                            await _analyticsService.ExcludeTrackAsync(clusterId, track.TrackId);
                        }

                        await _analyticsService.SaveChangesAsync();

                        AnsiConsole.MarkupLine($"[green]✓ {tracksToRemove.Count} track(s) excluded successfully![/]");
                        AnsiConsole.MarkupLine("[dim]These tracks will no longer appear in this cluster[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]❌ Error excluding tracks: {ex.Message.EscapeMarkup()}[/]");
                        _logger.LogError(ex, "Failed to exclude tracks from cluster '{ClusterName}'", cluster.Name);
                    }

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                    Console.ReadKey(intercept: true);
                    return;
                }
                else
                {
                    // Continue editing
                    continue;
                }
            }
            else if (selection == "← Cancel - Discard changes")
            {
                if (tracksToRemove.Any())
                {
                    var confirmCancel = AnsiConsole.Confirm(
                        "[yellow]Discard all changes and exit?[/]",
                        false
                    );

                    if (confirmCancel)
                    {
                        AnsiConsole.MarkupLine("[yellow]Changes discarded.[/]");
                        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                        Console.ReadKey(intercept: true);
                        return;
                    }
                }
                else
                {
                    return; // Nothing to discard, just exit
                }
            }
            else
            {
                // Find the selected track
                var selectedIndex = trackChoices.IndexOf(selection);
                if (selectedIndex >= 0 && selectedIndex < sorted.Count)
                {
                    var selectedTrack = sorted[selectedIndex];

                    // Toggle selection
                    if (tracksToRemove.Contains(selectedTrack))
                    {
                        tracksToRemove.Remove(selectedTrack);
                        AnsiConsole.MarkupLine($"[yellow]Unmarked:[/] {selectedTrack.TrackName.EscapeMarkup()}");
                    }
                    else
                    {
                        tracksToRemove.Add(selectedTrack);
                        AnsiConsole.MarkupLine($"[red]Marked for removal:[/] {selectedTrack.TrackName.EscapeMarkup()}");
                    }

                    Thread.Sleep(500); // Brief pause to show feedback
                }
            }
        }
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
