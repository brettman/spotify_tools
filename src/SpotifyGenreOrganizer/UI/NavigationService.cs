using Spectre.Console;
using SpotifyTools.Analytics;
using SpotifyTools.Domain.Entities;

namespace SpotifyGenreOrganizer.UI;

/// <summary>
/// Service for handling cascading navigation flows through artists, playlists, and tracks
/// </summary>
public class NavigationService
{
    private readonly IAnalyticsService _analyticsService;

    public NavigationService(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
    }

    /// <summary>
    /// Navigation flow: Paginated Artists Table → Select → Tracks → Track Detail
    /// </summary>
    public async Task NavigateByArtistAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[green]Browse by Artist[/]").RuleStyle("green"));
            AnsiConsole.WriteLine();

            // Step 1: Get all artists
            var artists = await _analyticsService.GetAllArtistsSortedByPopularityAsync();

            if (!artists.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No artists found in database.[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            // Step 2: Browse paginated table and select artist
            var selectedArtist = BrowsePaginatedArtists(artists);
            if (selectedArtist == null) return; // Back to main menu

            // Step 3: Get tracks for this artist
            AnsiConsole.Clear();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Loading tracks for {selectedArtist.Name.EscapeMarkup()}...", async ctx =>
                {
                    await Task.Delay(100); // Brief visual feedback
                });

            var tracks = await _analyticsService.GetTracksByArtistIdAsync(selectedArtist.Id);

            if (!tracks.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]No tracks found for {selectedArtist.Name.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                Console.ReadKey(intercept: true);
                continue;
            }

            // Step 4: User selects track
            var selectedTrack = MenuBuilder.SelectTrack(tracks, selectedArtist.Name);
            if (selectedTrack == null) continue; // Back to artist list

            // Step 5: Show detail report
            await ShowTrackDetailAsync(selectedTrack.Id);
        }
    }

    /// <summary>
    /// Browse paginated artists table with navigation controls
    /// </summary>
    private Artist? BrowsePaginatedArtists(List<Artist> allArtists)
    {
        const int pageSize = 30;
        int currentPage = 1;
        var sorted = allArtists.OrderBy(a => a.Name).ToList();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[green]Browse Artists[/]").RuleStyle("green"));
            AnsiConsole.WriteLine();

            // Render current page
            SpectreReportFormatter.RenderArtistsTablePage(sorted, currentPage, pageSize, out int totalPages);
            AnsiConsole.WriteLine();

            // Show navigation prompt
            AnsiConsole.MarkupLine("[cyan]Options:[/] [green][[N]]ext[/] [green][[P]]rev[/] [yellow][[J]]ump[/] [yellow][[1-30]][/] Select row [cyan][[S]]earch[/] [dim][[B]]ack[/]");
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Command:[/]")
                    .PromptStyle("green")
                    .AllowEmpty()
            ).Trim().ToLower();

            if (string.IsNullOrEmpty(input) || input == "b" || input == "back")
            {
                return null; // Back
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
            else if (input == "s" || input == "search")
            {
                return SearchAndSelectArtist(sorted);
            }
            else if (int.TryParse(input, out int rowNum) && rowNum >= 1 && rowNum <= pageSize)
            {
                // Select artist from current page
                var startIndex = (currentPage - 1) * pageSize;
                var pageItems = sorted.Skip(startIndex).Take(pageSize).ToList();

                if (rowNum <= pageItems.Count)
                {
                    return pageItems[rowNum - 1];
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

    /// <summary>
    /// Search for artist by name and select from results
    /// </summary>
    private Artist? SearchAndSelectArtist(List<Artist> allArtists)
    {
        var searchTerm = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Search artist name:[/]")
                .PromptStyle("green")
                .AllowEmpty()
        );

        if (string.IsNullOrWhiteSpace(searchTerm)) return null;

        var matches = allArtists
            .Where(a => a.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name)
            .ToList();

        if (!matches.Any())
        {
            AnsiConsole.MarkupLine($"\n[red]No artists found matching '{searchTerm.EscapeMarkup()}'[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return null;
        }

        if (matches.Count == 1)
        {
            AnsiConsole.MarkupLine($"\n[green]Selected:[/] {matches[0].Name.EscapeMarkup()}");
            return matches[0];
        }

        // Multiple matches - show selection
        AnsiConsole.WriteLine();
        return MenuBuilder.SelectArtist(matches);
    }

    /// <summary>
    /// Navigation flow: Paginated Playlists Table → Select → Tracks → Track Detail
    /// </summary>
    public async Task NavigateByPlaylistAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Browse by Playlist[/]").RuleStyle("blue"));
            AnsiConsole.WriteLine();

            // Step 1: Get all playlists
            var playlists = await _analyticsService.GetAllPlaylistsSortedByNameAsync();

            if (!playlists.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No playlists found in database.[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            // Step 2: Browse paginated table and select playlist
            var selectedPlaylist = BrowsePaginatedPlaylists(playlists);
            if (selectedPlaylist == null) return; // Back to main menu

            // Step 3: Get tracks in this playlist (preserve order)
            AnsiConsole.Clear();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Loading tracks from {selectedPlaylist.Name.EscapeMarkup()}...", async ctx =>
                {
                    await Task.Delay(100); // Brief visual feedback
                });

            var tracks = await _analyticsService.GetTracksByPlaylistIdAsync(selectedPlaylist.Id, preserveOrder: true);

            if (!tracks.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]No tracks in playlist: {selectedPlaylist.Name.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                Console.ReadKey(intercept: true);
                continue;
            }

            // Step 4: User selects track
            var selectedTrack = MenuBuilder.SelectTrack(tracks, selectedPlaylist.Name);
            if (selectedTrack == null) continue; // Back to playlist list

            // Step 5: Show detail report
            await ShowTrackDetailAsync(selectedTrack.Id);
        }
    }

    /// <summary>
    /// Browse paginated playlists table with navigation controls
    /// </summary>
    private Playlist? BrowsePaginatedPlaylists(List<Playlist> allPlaylists)
    {
        const int pageSize = 30;
        int currentPage = 1;
        var sorted = allPlaylists.OrderBy(p => p.Name).ToList();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Browse Playlists[/]").RuleStyle("blue"));
            AnsiConsole.WriteLine();

            // Render current page
            SpectreReportFormatter.RenderPlaylistsTablePage(sorted, currentPage, pageSize, out int totalPages);
            AnsiConsole.WriteLine();

            // Show navigation prompt
            AnsiConsole.MarkupLine("[cyan]Options:[/] [green][[N]]ext[/] [green][[P]]rev[/] [yellow][[J]]ump[/] [yellow][[1-30]][/] Select row [cyan][[S]]earch[/] [dim][[B]]ack[/]");
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Command:[/]")
                    .PromptStyle("green")
                    .AllowEmpty()
            ).Trim().ToLower();

            if (string.IsNullOrEmpty(input) || input == "b" || input == "back")
            {
                return null; // Back
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
            else if (input == "s" || input == "search")
            {
                return SearchAndSelectPlaylist(sorted);
            }
            else if (int.TryParse(input, out int rowNum) && rowNum >= 1 && rowNum <= pageSize)
            {
                // Select playlist from current page
                var startIndex = (currentPage - 1) * pageSize;
                var pageItems = sorted.Skip(startIndex).Take(pageSize).ToList();

                if (rowNum <= pageItems.Count)
                {
                    return pageItems[rowNum - 1];
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

    /// <summary>
    /// Search for playlist by name and select from results
    /// </summary>
    private Playlist? SearchAndSelectPlaylist(List<Playlist> allPlaylists)
    {
        var searchTerm = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Search playlist name:[/]")
                .PromptStyle("green")
                .AllowEmpty()
        );

        if (string.IsNullOrWhiteSpace(searchTerm)) return null;

        var matches = allPlaylists
            .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .ToList();

        if (!matches.Any())
        {
            AnsiConsole.MarkupLine($"\n[red]No playlists found matching '{searchTerm.EscapeMarkup()}'[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(intercept: true);
            return null;
        }

        if (matches.Count == 1)
        {
            AnsiConsole.MarkupLine($"\n[green]Selected:[/] {matches[0].Name.EscapeMarkup()}");
            return matches[0];
        }

        // Multiple matches - show selection
        AnsiConsole.WriteLine();
        return MenuBuilder.SelectPlaylist(matches);
    }

    /// <summary>
    /// Navigation flow: Search Text → Select Track → Track Detail
    /// </summary>
    public async Task NavigateBySearchAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[cyan]Search by Name[/]").RuleStyle("cyan"));
            AnsiConsole.WriteLine();

            // Step 1: Prompt for search term
            var searchTerm = MenuBuilder.PromptTrackSearch();

            // Step 2: Search tracks
            AnsiConsole.WriteLine();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Searching for '{searchTerm.EscapeMarkup()}'...", async ctx =>
                {
                    await Task.Delay(100); // Brief visual feedback
                });

            var results = await _analyticsService.SearchTracksAsync(searchTerm, 50);

            if (!results.Any())
            {
                AnsiConsole.MarkupLine($"\n[red]No tracks found matching '{searchTerm.EscapeMarkup()}'[/]");

                var retry = AnsiConsole.Confirm("Try another search?", defaultValue: true);
                if (!retry) return;
                continue;
            }

            // Step 3: Display results with SelectionPrompt
            var choices = results
                .Select(r => r.DisplayName)
                .Prepend("[dim]← Search again[/]")
                .ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[green]Found {results.Count} track(s)[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up/down for more)[/]")
                    .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                    .AddChoices(choices)
            );

            if (selection.StartsWith("[dim]")) continue; // Search again

            // Step 4: Get selected track ID
            var selectedResult = results.First(r => r.DisplayName == selection);

            // Step 5: Show detail report
            await ShowTrackDetailAsync(selectedResult.TrackId);
        }
    }

    /// <summary>
    /// Displays track detail report (shared by all navigation flows)
    /// </summary>
    private async Task ShowTrackDetailAsync(string trackId)
    {
        AnsiConsole.Clear();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading track details...", async ctx =>
            {
                await Task.Delay(100); // Brief visual feedback
            });

        var report = await _analyticsService.GetTrackDetailReportAsync(trackId);

        if (report == null)
        {
            AnsiConsole.MarkupLine("[red]Could not load track details[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
            Console.ReadKey(intercept: true);
            return;
        }

        AnsiConsole.Clear();

        // Render with rich Spectre components
        SpectreReportFormatter.RenderTrackDetailReport(report);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
        Console.ReadKey(intercept: true);
    }
}
