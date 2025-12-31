using System.Text;

namespace SpotifyTools.Analytics;

/// <summary>
/// Formats analytics reports for console display
/// </summary>
public static class ReportFormatter
{
    public static string FormatTrackDetailReport(TrackDetailReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine();
        sb.AppendLine("╔════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                        TRACK DETAIL REPORT                             ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Track Info
        sb.AppendLine($"📀 TRACK: {report.Name}");
        sb.AppendLine($"   Duration: {report.FormattedDuration}");
        sb.AppendLine($"   Popularity: {report.Popularity}/100");
        sb.AppendLine($"   Explicit: {(report.Explicit ? "Yes" : "No")}");
        if (!string.IsNullOrEmpty(report.Isrc))
            sb.AppendLine($"   ISRC: {report.Isrc}");
        if (report.AddedAt.HasValue)
            sb.AppendLine($"   Added to Library: {report.AddedAt.Value:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // Artists
        if (report.Artists.Any())
        {
            sb.AppendLine($"🎤 ARTIST{(report.Artists.Count > 1 ? "S" : "")}:");
            foreach (var artist in report.Artists)
            {
                sb.AppendLine($"   • {artist.Name}");
                if (artist.Genres.Any())
                    sb.AppendLine($"     Genres: {string.Join(", ", artist.Genres)}");
                sb.AppendLine($"     Popularity: {artist.Popularity}/100 | Followers: {artist.Followers:N0}");
            }
            sb.AppendLine();
        }

        // Album
        if (report.Album != null)
        {
            sb.AppendLine("💿 ALBUM:");
            sb.AppendLine($"   {report.Album.Name}");
            sb.AppendLine($"   Type: {report.Album.AlbumType} | Tracks: {report.Album.TotalTracks}");
            if (report.Album.ReleaseDate.HasValue)
                sb.AppendLine($"   Released: {report.Album.ReleaseDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(report.Album.Label))
                sb.AppendLine($"   Label: {report.Album.Label}");
            sb.AppendLine();
        }

        // Audio Features
        if (report.AudioFeatures != null)
        {
            var af = report.AudioFeatures;

            sb.AppendLine("🎵 AUDIO FEATURES:");
            sb.AppendLine();

            // Musical characteristics
            sb.AppendLine("   ┌─────────────────────────────────────────┐");
            sb.AppendLine("   │ MUSICAL CHARACTERISTICS                 │");
            sb.AppendLine("   ├─────────────────────────────────────────┤");
            sb.AppendLine($"   │ Tempo:          {af.Tempo,6:F1} BPM            │");
            sb.AppendLine($"   │ Key:            {af.KeyName,-18} │");
            sb.AppendLine($"   │ Mode:           {af.ModeName,-18} │");
            sb.AppendLine($"   │ Time Signature: {af.TimeSignatureDisplay,-18} │");
            sb.AppendLine($"   │ Loudness:       {af.Loudness,6:F1} dB             │");
            sb.AppendLine("   └─────────────────────────────────────────┘");
            sb.AppendLine();

            // Mood and feel (0-1 scale)
            sb.AppendLine("   ┌─────────────────────────────────────────┐");
            sb.AppendLine("   │ MOOD & FEEL                             │");
            sb.AppendLine("   ├─────────────────────────────────────────┤");
            sb.AppendLine($"   │ Danceability:   {FormatBar(af.Danceability)} │");
            sb.AppendLine($"   │ Energy:         {FormatBar(af.Energy)} │");
            sb.AppendLine($"   │ Valence:        {FormatBar(af.Valence)} │");
            sb.AppendLine("   └─────────────────────────────────────────┘");
            sb.AppendLine();

            // Audio qualities (0-1 scale)
            sb.AppendLine("   ┌─────────────────────────────────────────┐");
            sb.AppendLine("   │ AUDIO QUALITIES                         │");
            sb.AppendLine("   ├─────────────────────────────────────────┤");
            sb.AppendLine($"   │ Acousticness:   {FormatBar(af.Acousticness)} │");
            sb.AppendLine($"   │ Instrumental:   {FormatBar(af.Instrumentalness)} │");
            sb.AppendLine($"   │ Liveness:       {FormatBar(af.Liveness)} │");
            sb.AppendLine($"   │ Speechiness:    {FormatBar(af.Speechiness)} │");
            sb.AppendLine("   └─────────────────────────────────────────┘");
            sb.AppendLine();
        }

        // Audio Analysis (section-by-section breakdown)
        if (report.AudioAnalysis != null && report.AudioAnalysis.Sections.Any())
        {
            var analysis = report.AudioAnalysis;

            sb.AppendLine("🎼 AUDIO ANALYSIS (Section-by-Section):");
            sb.AppendLine();
            sb.AppendLine("   Overall Track Analysis:");
            sb.AppendLine($"   • Overall Tempo: {analysis.TrackTempo:F1} BPM");
            sb.AppendLine($"   • Overall Key: {analysis.KeyName} {analysis.ModeName}");
            sb.AppendLine($"   • Time Signature: {analysis.TimeSignatureDisplay}");
            sb.AppendLine($"   • Sections: {analysis.Sections.Count}");
            sb.AppendLine();

            sb.AppendLine("   Section Breakdown:");
            sb.AppendLine("   ┌────────┬─────────┬──────┬─────────────────┬──────┐");
            sb.AppendLine("   │ Time   │ Tempo   │ Key  │ Mode            │ Sig  │");
            sb.AppendLine("   ├────────┼─────────┼──────┼─────────────────┼──────┤");

            // Track key/tempo/time signature changes
            int? lastKey = null;
            int? lastMode = null;
            float? lastTempo = null;
            int? lastTimeSig = null;

            foreach (var section in analysis.Sections)
            {
                // Highlight changes
                var keyChanged = lastKey.HasValue && section.Key != lastKey.Value;
                var modeChanged = lastMode.HasValue && section.Mode != lastMode.Value;
                var tempoChanged = lastTempo.HasValue && Math.Abs(section.Tempo - lastTempo.Value) > 5;
                var timeSigChanged = lastTimeSig.HasValue && section.TimeSignature != lastTimeSig.Value;

                var highlight = keyChanged || modeChanged || tempoChanged || timeSigChanged;

                var timeStr = section.StartTime.PadLeft(6);
                var tempoStr = $"{section.Tempo:F1}".PadLeft(7);
                var keyStr = section.KeyName.PadRight(4);
                var modeStr = section.ModeName.PadRight(15);
                var sigStr = section.TimeSignatureDisplay.PadLeft(4);

                var prefix = highlight ? " ► " : "   ";
                sb.AppendLine($"{prefix}│ {timeStr} │ {tempoStr} │ {keyStr} │ {modeStr} │ {sigStr} │");

                lastKey = section.Key;
                lastMode = section.Mode;
                lastTempo = section.Tempo;
                lastTimeSig = section.TimeSignature;
            }

            sb.AppendLine("   └────────┴─────────┴──────┴─────────────────┴──────┘");
            sb.AppendLine("   (► indicates change from previous section)");
            sb.AppendLine();
        }

        // Playlists
        if (report.Playlists.Any())
        {
            sb.AppendLine($"📋 PLAYLISTS ({report.Playlists.Count}):");
            foreach (var playlist in report.Playlists.Take(10))
            {
                sb.AppendLine($"   • {playlist}");
            }
            if (report.Playlists.Count > 10)
                sb.AppendLine($"   ... and {report.Playlists.Count - 10} more");
            sb.AppendLine();
        }

        sb.AppendLine("────────────────────────────────────────────────────────────────────────");

        return sb.ToString();
    }

    private static string FormatBar(float value)
    {
        const int barLength = 20;
        var filled = (int)(value * barLength);
        var bar = new string('█', filled) + new string('░', barLength - filled);
        return $"{bar} {value:P0}";
    }
}
