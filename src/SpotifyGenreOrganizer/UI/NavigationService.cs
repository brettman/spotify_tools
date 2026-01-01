using Spectre.Console;
using SpotifyTools.Analytics;

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
    /// Navigation flow: Artist → Tracks → Track Detail
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

            // Step 2: User selects artist
            var selectedArtist = MenuBuilder.SelectArtist(artists);
            if (selectedArtist == null) return; // Back pressed

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
    /// Navigation flow: Playlist → Tracks → Track Detail
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

            // Step 2: User selects playlist
            var selectedPlaylist = MenuBuilder.SelectPlaylist(playlists);
            if (selectedPlaylist == null) return; // Back pressed

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
