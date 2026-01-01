using Spectre.Console;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Domain.Enums;

namespace SpotifyGenreOrganizer.UI;

/// <summary>
/// Formats data for display using Spectre.Console components
/// </summary>
public static class SpectreReportFormatter
{
    /// <summary>
    /// Renders a sync history table with the last N syncs
    /// </summary>
    public static void RenderSyncHistoryTable(List<SyncHistory> history)
    {
        if (!history.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No sync history found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Title("[cyan bold]📋 Sync History - Last 10[/]")
            .AddColumn(new TableColumn("[yellow]ID[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Date[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Type[/]").Centered())
            .AddColumn(new TableColumn("[magenta]Status[/]").Centered())
            .AddColumn(new TableColumn("[cyan]Tracks[/]").RightAligned())
            .AddColumn(new TableColumn("[yellow]Artists[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Albums[/]").RightAligned())
            .AddColumn(new TableColumn("[blue]Playlists[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]Duration[/]").RightAligned());

        foreach (var sync in history)
        {
            var statusMarkup = GetStatusMarkup(sync.Status);
            var duration = sync.CompletedAt.HasValue && sync.StartedAt != default
                ? (sync.CompletedAt.Value - sync.StartedAt).ToString(@"hh\:mm\:ss")
                : "-";

            table.AddRow(
                sync.Id.ToString(),
                sync.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                sync.SyncType.ToString(),
                statusMarkup,
                sync.TracksAdded.ToString(),
                sync.ArtistsAdded.ToString(),
                sync.AlbumsAdded.ToString(),
                sync.PlaylistsSynced.ToString(),
                duration
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Gets status markup with emoji and color
    /// </summary>
    private static string GetStatusMarkup(SyncStatus status)
    {
        return status switch
        {
            SyncStatus.Success => "[green]✓ Success[/]",
            SyncStatus.Failed => "[red]❌ Failed[/]",
            SyncStatus.InProgress => "[yellow]🔄 In Progress[/]",
            _ => "[dim]Unknown[/]"
        };
    }
}
