namespace SpotifyTools.Web.DTOs;

public class ArtistSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
}
