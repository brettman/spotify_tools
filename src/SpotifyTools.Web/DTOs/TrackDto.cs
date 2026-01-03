namespace SpotifyTools.Web.DTOs;

public class TrackDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ArtistSummaryDto> Artists { get; set; } = new();
    public string? AlbumName { get; set; }
    public int DurationMs { get; set; }
    public int Popularity { get; set; }
    public List<string> Genres { get; set; } = new();
    public bool Explicit { get; set; }
    public DateTime? AddedAt { get; set; }
}
