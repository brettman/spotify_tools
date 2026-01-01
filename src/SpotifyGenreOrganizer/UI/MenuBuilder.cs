using Spectre.Console;

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
                    "Search by Name",
                    "Back to Main Menu"
                })
        );
    }
}
