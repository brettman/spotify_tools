namespace SpotifyTools.Web.DTOs;

/// <summary>
/// Track with play count statistics
/// </summary>
public class TrackPlayCountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artists { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public DateTime LastPlayed { get; set; }
    public int DurationMs { get; set; }
    public int Popularity { get; set; }
}

/// <summary>
/// Artist with play count statistics
/// </summary>
public class ArtistPlayCountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
    public DateTime LastPlayed { get; set; }
}

/// <summary>
/// Genre with play count statistics
/// </summary>
public class GenrePlayCountDto
{
    public string Genre { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
    public int UniqueArtistCount { get; set; }
}

/// <summary>
/// Play count by date
/// </summary>
public class PlaysByDateDto
{
    public DateTime Date { get; set; }
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
}

/// <summary>
/// Play count by hour of day (0-23)
/// </summary>
public class PlaysByHourDto
{
    public int Hour { get; set; }
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
}

/// <summary>
/// Play count by day of week
/// </summary>
public class PlaysByDayOfWeekDto
{
    public int DayOfWeek { get; set; } // 0=Sunday, 6=Saturday
    public string DayName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
}

/// <summary>
/// Context type (playlist, album, artist, etc.) play statistics
/// </summary>
public class PlaysByContextDto
{
    public string ContextType { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int UniqueTrackCount { get; set; }
}

/// <summary>
/// Recent play activity
/// </summary>
public class RecentPlayDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public string Artists { get; set; } = string.Empty;
    public string? ContextType { get; set; }
    public int DurationMs { get; set; }
}

/// <summary>
/// Overall listening statistics
/// </summary>
public class ListeningStatsDto
{
    public int TotalPlays { get; set; }
    public int UniqueTracksPlayed { get; set; }
    public int UniqueArtistsPlayed { get; set; }
    public long TotalListeningTimeMs { get; set; }
    public DateTime? FirstPlay { get; set; }
    public DateTime? LastPlay { get; set; }
    public int DaysTracked { get; set; }
    public double AveragePlaysPerDay { get; set; }
}

/// <summary>
/// Time range for filtering analytics
/// </summary>
public enum TimeRange
{
    AllTime,
    Last7Days,
    Last30Days,
    Last90Days,
    Last365Days,
    ThisWeek,
    ThisMonth,
    ThisYear
}
