using Spectre.Console;
using Spectre.Console.Rendering;
using SpotifyTools.Domain.Entities;
using SpotifyTools.Domain.Enums;
using SpotifyTools.Analytics;

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

    /// <summary>
    /// Renders a comprehensive track detail report with rich Spectre components
    /// </summary>
    public static void RenderTrackDetailReport(TrackDetailReport report)
    {
        // Header Panel - Track name, duration, popularity
        var headerContent = new Markup(
            $"[bold]{report.Name.EscapeMarkup()}[/]\n" +
            $"[dim]Duration:[/] {report.FormattedDuration}  " +
            $"[dim]Popularity:[/] {RenderPopularityBar(report.Popularity)}  " +
            $"[dim]Explicit:[/] {(report.Explicit ? "[red]Yes[/]" : "[green]No[/]")}"
        );

        var headerPanel = new Panel(headerContent)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Header("[green bold]🎵 Track Details[/]");

        AnsiConsole.Write(headerPanel);
        AnsiConsole.WriteLine();

        // Build left and right columns
        var leftColumn = BuildLeftColumn(report);
        var rightColumn = BuildRightColumn(report);

        // Two-column layout
        var columns = new Columns(new IRenderable[] { leftColumn, rightColumn })
            .Collapse();

        AnsiConsole.Write(columns);
        AnsiConsole.WriteLine();

        // Audio Analysis (full-width at bottom if available)
        if (report.AudioAnalysis != null && report.AudioAnalysis.Sections.Any())
        {
            RenderAudioAnalysis(report.AudioAnalysis);
        }
    }

    /// <summary>
    /// Builds the left column: Artists, Album, Playlists
    /// </summary>
    private static IRenderable BuildLeftColumn(TrackDetailReport report)
    {
        var panels = new List<IRenderable>();

        // Artists Table
        if (report.Artists.Any())
        {
            var artistsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .Title("[yellow bold]👥 Artists[/]")
                .AddColumn(new TableColumn("[cyan]Name[/]").LeftAligned())
                .AddColumn(new TableColumn("[green]Genres[/]").LeftAligned())
                .AddColumn(new TableColumn("[magenta]Pop[/]").RightAligned())
                .AddColumn(new TableColumn("[blue]Followers[/]").RightAligned());

            foreach (var artist in report.Artists)
            {
                artistsTable.AddRow(
                    artist.Name.EscapeMarkup(),
                    artist.Genres.Any() ? string.Join(", ", artist.Genres.Take(3)).EscapeMarkup() : "[dim]none[/]",
                    artist.Popularity.ToString(),
                    $"{artist.Followers:N0}"
                );
            }

            panels.Add(artistsTable);
        }

        // Album Panel
        if (report.Album != null)
        {
            var albumText = new Markup(
                $"[bold]{report.Album.Name.EscapeMarkup()}[/]\n" +
                $"[dim]Type:[/] {report.Album.AlbumType.EscapeMarkup()}\n" +
                $"[dim]Released:[/] {report.Album.ReleaseDate?.ToString("yyyy-MM-dd") ?? "Unknown"}\n" +
                $"[dim]Label:[/] {report.Album.Label?.EscapeMarkup() ?? "[dim]Unknown[/]"}\n" +
                $"[dim]Tracks:[/] {report.Album.TotalTracks}"
            );

            var albumPanel = new Panel(albumText)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue)
                .Header("[blue bold]💿 Album[/]");

            panels.Add(albumPanel);
        }

        // Playlists Panel
        if (report.Playlists.Any())
        {
            var playlistText = string.Join("\n", report.Playlists.Take(10).Select(p => $"• {p.EscapeMarkup()}"));
            if (report.Playlists.Count > 10)
            {
                playlistText += $"\n[dim]... and {report.Playlists.Count - 10} more[/]";
            }

            var playlistPanel = new Panel(new Markup(playlistText))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Magenta)
                .Header($"[magenta bold]📂 Playlists ({report.Playlists.Count})[/]");

            panels.Add(playlistPanel);
        }

        return new Rows(panels);
    }

    /// <summary>
    /// Builds the right column: Audio Features
    /// </summary>
    private static IRenderable BuildRightColumn(TrackDetailReport report)
    {
        var panels = new List<IRenderable>();

        if (report.AudioFeatures != null)
        {
            var af = report.AudioFeatures;

            // Musical Properties Table
            var musicalTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Cyan)
                .Title("[cyan bold]🎼 Musical Properties[/]")
                .AddColumn(new TableColumn("[yellow]Property[/]").LeftAligned())
                .AddColumn(new TableColumn("[green]Value[/]").LeftAligned());

            musicalTable.AddRow("Tempo", $"{af.Tempo:F1} BPM");
            musicalTable.AddRow("Key", af.KeyName);
            musicalTable.AddRow("Mode", af.ModeName);
            musicalTable.AddRow("Time Signature", af.TimeSignatureDisplay);
            musicalTable.AddRow("Loudness", $"{af.Loudness:F1} dB");

            panels.Add(musicalTable);

            // Mood BarChart
            var moodChart = new BarChart()
                .Width(40)
                .Label("[bold]😊 Mood & Energy[/]")
                .CenterLabel();

            moodChart.AddItem("Danceability", (int)(af.Danceability * 100), Color.Green);
            moodChart.AddItem("Energy", (int)(af.Energy * 100), Color.Red);
            moodChart.AddItem("Valence", (int)(af.Valence * 100), Color.Yellow);

            var moodPanel = new Panel(moodChart)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green);

            panels.Add(moodPanel);

            // Qualities BarChart
            var qualitiesChart = new BarChart()
                .Width(40)
                .Label("[bold]🎹 Audio Qualities[/]")
                .CenterLabel();

            qualitiesChart.AddItem("Acousticness", (int)(af.Acousticness * 100), Color.Blue);
            qualitiesChart.AddItem("Instrumental", (int)(af.Instrumentalness * 100), Color.Cyan1);
            qualitiesChart.AddItem("Liveness", (int)(af.Liveness * 100), Color.Magenta);
            qualitiesChart.AddItem("Speechiness", (int)(af.Speechiness * 100), Color.Orange1);

            var qualitiesPanel = new Panel(qualitiesChart)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);

            panels.Add(qualitiesPanel);
        }
        else
        {
            // No audio features available
            var noDataPanel = new Panel(new Markup("[dim]Audio features not available\n(Spotify API deprecated)[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Header("[grey]🎼 Audio Features[/]");

            panels.Add(noDataPanel);
        }

        return new Rows(panels);
    }

    /// <summary>
    /// Renders audio analysis section table (full-width)
    /// </summary>
    private static void RenderAudioAnalysis(TrackDetailReport.AudioAnalysisInfo analysis)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Title($"[cyan bold]🎚️  Audio Analysis - {analysis.Sections.Count} Sections[/]")
            .AddColumn(new TableColumn("[yellow]#[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Start[/]").RightAligned())
            .AddColumn(new TableColumn("[blue]Duration[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]Tempo[/]").RightAligned())
            .AddColumn(new TableColumn("[cyan]Key[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Mode[/]").Centered())
            .AddColumn(new TableColumn("[green]Time Sig[/]").Centered())
            .AddColumn(new TableColumn("[red]Loudness[/]").RightAligned())
            .AddColumn(new TableColumn("[white]Changes[/]").LeftAligned());

        int? previousKey = null;
        int? previousMode = null;
        float? previousTempo = null;
        int? previousTimeSignature = null;

        for (int i = 0; i < analysis.Sections.Count; i++)
        {
            var section = analysis.Sections[i];
            var changes = new List<string>();

            // Detect changes
            if (previousKey.HasValue && section.Key != previousKey.Value)
                changes.Add("►Key");
            if (previousMode.HasValue && section.Mode != previousMode.Value)
                changes.Add("►Mode");
            if (previousTempo.HasValue && Math.Abs(section.Tempo - previousTempo.Value) > 5)
                changes.Add("►Tempo");
            if (previousTimeSignature.HasValue && section.TimeSignature != previousTimeSignature.Value)
                changes.Add("►TimeSig");

            table.AddRow(
                (i + 1).ToString(),
                section.StartTime,
                $"{section.Duration:F1}s",
                $"{section.Tempo:F0}",
                section.KeyName,
                section.ModeName,
                section.TimeSignatureDisplay,
                $"{section.Loudness:F1}",
                changes.Any() ? $"[yellow]{string.Join(" ", changes)}[/]" : ""
            );

            // Update previous values
            previousKey = section.Key;
            previousMode = section.Mode;
            previousTempo = section.Tempo;
            previousTimeSignature = section.TimeSignature;
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders a popularity value as a colored bar with percentage
    /// </summary>
    private static string RenderPopularityBar(int popularity)
    {
        var color = popularity switch
        {
            >= 80 => "green",
            >= 60 => "yellow",
            >= 40 => "orange1",
            _ => "red"
        };

        var barLength = popularity / 5; // 0-20 characters
        var bar = new string('█', barLength);

        return $"[{color}]{bar}[/] [dim]{popularity}%[/]";
    }

    /// <summary>
    /// Renders a paginated table of artists
    /// </summary>
    public static void RenderArtistsTablePage(List<Artist> allArtists, int page, int pageSize, out int totalPages)
    {
        if (!allArtists.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No artists found.[/]");
            totalPages = 0;
            return;
        }

        // Sort alphabetically for better browsing
        var sorted = allArtists.OrderBy(a => a.Name).ToList();
        totalPages = (int)Math.Ceiling(sorted.Count / (double)pageSize);

        // Clamp page to valid range
        page = Math.Max(1, Math.Min(page, totalPages));

        var startIndex = (page - 1) * pageSize;
        var pageItems = sorted.Skip(startIndex).Take(pageSize).ToList();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Title($"[yellow bold]👥 Artists - Page {page}/{totalPages}[/] [dim]({sorted.Count} total)[/]")
            .AddColumn(new TableColumn("[cyan]#[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Artist Name[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Followers[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]Pop[/]").RightAligned())
            .AddColumn(new TableColumn("[yellow]Genres[/]").LeftAligned());

        int rowNum = 1;
        foreach (var artist in pageItems)
        {
            var genresDisplay = artist.Genres.Any()
                ? string.Join(", ", artist.Genres.Take(3)).EscapeMarkup()
                : "[dim]none[/]";

            if (artist.Genres.Length > 3)
            {
                genresDisplay += $" [dim](+{artist.Genres.Length - 3})[/]";
            }

            table.AddRow(
                rowNum.ToString(),
                artist.Name.EscapeMarkup(),
                $"{artist.Followers:N0}",
                artist.Popularity.ToString(),
                genresDisplay
            );
            rowNum++;
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Renders a paginated table of playlists
    /// </summary>
    public static void RenderPlaylistsTablePage(List<Playlist> allPlaylists, int page, int pageSize, out int totalPages)
    {
        if (!allPlaylists.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No playlists found.[/]");
            totalPages = 0;
            return;
        }

        // Sort alphabetically
        var sorted = allPlaylists.OrderBy(p => p.Name).ToList();
        totalPages = (int)Math.Ceiling(sorted.Count / (double)pageSize);

        // Clamp page to valid range
        page = Math.Max(1, Math.Min(page, totalPages));

        var startIndex = (page - 1) * pageSize;
        var pageItems = sorted.Skip(startIndex).Take(pageSize).ToList();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Title($"[blue bold]📂 Playlists - Page {page}/{totalPages}[/] [dim]({sorted.Count} total)[/]")
            .AddColumn(new TableColumn("[cyan]#[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Playlist Name[/]").LeftAligned())
            .AddColumn(new TableColumn("[yellow]Tracks[/]").RightAligned())
            .AddColumn(new TableColumn("[magenta]Owner[/]").LeftAligned())
            .AddColumn(new TableColumn("[blue]Description[/]").LeftAligned());

        int rowNum = 1;
        foreach (var playlist in pageItems)
        {
            var description = !string.IsNullOrEmpty(playlist.Description)
                ? (playlist.Description.Length > 40
                    ? playlist.Description.Substring(0, 37).EscapeMarkup() + "..."
                    : playlist.Description.EscapeMarkup())
                : "[dim]none[/]";

            table.AddRow(
                rowNum.ToString(),
                playlist.Name.EscapeMarkup(),
                playlist.PlaylistTracks.Count.ToString(),
                playlist.OwnerId.EscapeMarkup(),
                description
            );
            rowNum++;
        }

        AnsiConsole.Write(table);
    }
}
