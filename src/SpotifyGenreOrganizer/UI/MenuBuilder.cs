using Spectre.Console;
using SpotifyTools.Domain.Entities;

namespace SpotifyGenreOrganizer.UI;

/// <summary>
/// Factory class for creating Spectre.Console menus with consistent styling
/// </summary>
public static class MenuBuilder
{
    /// <summary>
    /// Shows the main menu with 7 options
    /// </summary>
    public static string ShowMainMenu()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green bold]╔══════════════════════════════════════╗\n║   Spotify Tools - Main Menu         ║\n╚══════════════════════════════════════╝[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .AddChoices(new[] {
                    "Full Sync (Import all data)",
                    "Partial Sync (Select stages)",
                    "View Last Sync Status",
                    "View Sync History",
                    "Track Detail Report",
                    "Test Artist API (Debug)",
                    "Exit"
                })
        );
    }

    /// <summary>
    /// Shows the partial sync sub-menu for selecting individual stages
    /// </summary>
    public static string ShowPartialSyncMenu()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Sync Stage[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(new[] {
                    "Tracks",
                    "Artists",
                    "Albums",
                    "Playlists",
                    "[dim]Audio Features (disabled - API deprecated)[/]",
                    "Back to Main Menu"
                })
        );
    }

    /// <summary>
    /// Shows the navigation menu for selecting how to browse tracks
    /// </summary>
    public static string ShowNavigationMenu()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]How would you like to browse tracks?[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan, decoration: Decoration.Bold))
                .AddChoices(new[] {
                    "Browse by Artist",
                    "Browse by Playlist",
                    "Browse by Genre",
                    "Search by Name",
                    "Back to Main Menu"
                })
        );
    }

    /// <summary>
    /// Shows artist selection with follower counts
    /// </summary>
    public static Artist? SelectArtist(List<Artist> artists)
    {
        if (!artists.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No artists found in database.[/]");
            return null;
        }

        var choices = artists
            .Select(a => $"{a.Name.EscapeMarkup()} [dim]({a.Followers:N0} followers)[/]")
            .Prepend("[dim]← Back[/]")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[green]Select Artist ({artists.Count} total)[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down for more artists)[/]")
                .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                .AddChoices(choices)
        );

        if (selection.StartsWith("[dim]")) return null;

        // Extract artist name from the selection (before the follower count)
        var artistName = selection.Split(new[] { " [dim]" }, StringSplitOptions.None)[0];
        return artists.FirstOrDefault(a => a.Name.EscapeMarkup() == artistName);
    }

    /// <summary>
    /// Shows playlist selection
    /// </summary>
    public static Playlist? SelectPlaylist(List<Playlist> playlists)
    {
        if (!playlists.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No playlists found in database.[/]");
            return null;
        }

        var choices = playlists
            .Select(p => p.Name.EscapeMarkup())
            .Prepend("[dim]← Back[/]")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[blue]Select Playlist ({playlists.Count} total)[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down for more playlists)[/]")
                .HighlightStyle(new Style(Color.Blue, decoration: Decoration.Bold))
                .AddChoices(choices)
        );

        if (selection.StartsWith("[dim]")) return null;

        return playlists.FirstOrDefault(p => p.Name.EscapeMarkup() == selection);
    }

    /// <summary>
    /// Shows track selection with context (artist or playlist name)
    /// </summary>
    public static Track? SelectTrack(List<Track> tracks, string context)
    {
        if (!tracks.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No tracks found.[/]");
            return null;
        }

        var choices = tracks
            .Select(t => $"{t.Name.EscapeMarkup()} [dim]({FormatDuration(t.DurationMs)})[/]")
            .Prepend("[dim]← Back[/]")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[cyan]Select Track from {context.EscapeMarkup()} ({tracks.Count} tracks)[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up/down for more tracks)[/]")
                .HighlightStyle(new Style(Color.Cyan, decoration: Decoration.Bold))
                .AddChoices(choices)
        );

        if (selection.StartsWith("[dim]")) return null;

        // Extract track name from selection (before duration)
        var trackName = selection.Split(new[] { " [dim]" }, StringSplitOptions.None)[0];
        return tracks.FirstOrDefault(t => t.Name.EscapeMarkup() == trackName);
    }

    /// <summary>
    /// Prompts for track search text
    /// </summary>
    public static string PromptTrackSearch()
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Enter track name to search:[/]")
                .PromptStyle("green")
                .ValidationErrorMessage("[red]Search term cannot be empty[/]")
                .Validate(term => !string.IsNullOrWhiteSpace(term))
        );
    }

    /// <summary>
    /// Formats duration in milliseconds to mm:ss or h:mm:ss
    /// </summary>
    private static string FormatDuration(int durationMs)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}
