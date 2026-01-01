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
    /// Navigation flow: View All Artists Table → Search/Select → Tracks → Track Detail
    /// </summary>
    public async Task NavigateByArtistAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[green]Browse by Artist[/]").RuleStyle("green"));
            AnsiConsole.WriteLine();

            // Step 1: Get all artists and show comprehensive table
            var artists = await _analyticsService.GetAllArtistsSortedByPopularityAsync();

            if (!artists.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No artists found in database.[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            // Show full table for visualization
            SpectreReportFormatter.RenderArtistsTable(artists);
            AnsiConsole.WriteLine();

            // Step 2: Search/filter to select artist
            var searchTerm = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter artist name to view tracks[/] [dim](or 'back' to return)[/]:")
                    .PromptStyle("green")
                    .AllowEmpty()
            );

            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                return; // Back to main menu
            }

            // Filter artists by search term
            var matches = artists
                .Where(a => a.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name)
                .ToList();

            if (!matches.Any())
            {
                AnsiConsole.MarkupLine($"\n[red]No artists found matching '{searchTerm.EscapeMarkup()}'[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to try again...[/]");
                Console.ReadKey(intercept: true);
                continue;
            }

            // If multiple matches, let user select
            Artist selectedArtist;
            if (matches.Count == 1)
            {
                selectedArtist = matches[0];
                AnsiConsole.MarkupLine($"\n[green]Selected:[/] {selectedArtist.Name.EscapeMarkup()}");
            }
            else
            {
                AnsiConsole.WriteLine();
                selectedArtist = MenuBuilder.SelectArtist(matches)!;
                if (selectedArtist == null) continue; // Back to search
            }

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
    /// Navigation flow: View All Playlists Table → Search/Select → Tracks → Track Detail
    /// </summary>
    public async Task NavigateByPlaylistAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Browse by Playlist[/]").RuleStyle("blue"));
            AnsiConsole.WriteLine();

            // Step 1: Get all playlists and show comprehensive table
            var playlists = await _analyticsService.GetAllPlaylistsSortedByNameAsync();

            if (!playlists.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No playlists found in database.[/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            // Show full table for visualization
            SpectreReportFormatter.RenderPlaylistsTable(playlists);
            AnsiConsole.WriteLine();

            // Step 2: Search/filter to select playlist
            var searchTerm = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter playlist name to view tracks[/] [dim](or 'back' to return)[/]:")
                    .PromptStyle("green")
                    .AllowEmpty()
            );

            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                return; // Back to main menu
            }

            // Filter playlists by search term
            var matches = playlists
                .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name)
                .ToList();

            if (!matches.Any())
            {
                AnsiConsole.MarkupLine($"\n[red]No playlists found matching '{searchTerm.EscapeMarkup()}'[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to try again...[/]");
                Console.ReadKey(intercept: true);
                continue;
            }

            // If multiple matches, let user select
            Playlist selectedPlaylist;
            if (matches.Count == 1)
            {
                selectedPlaylist = matches[0];
                AnsiConsole.MarkupLine($"\n[green]Selected:[/] {selectedPlaylist.Name.EscapeMarkup()}");
            }
            else
            {
                AnsiConsole.WriteLine();
                selectedPlaylist = MenuBuilder.SelectPlaylist(matches)!;
                if (selectedPlaylist == null) continue; // Back to search
            }

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
