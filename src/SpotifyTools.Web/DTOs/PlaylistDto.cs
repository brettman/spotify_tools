namespace SpotifyTools.Web.DTOs;

public class PlaylistDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TrackCount { get; set; }
    public bool IsPublic { get; set; }
    public string? SpotifyId { get; set; }
}

public class PlaylistDetailDto : PlaylistDto
{
    public List<TrackDto> Tracks { get; set; } = new();
    public int TotalDurationMs { get; set; }
}
