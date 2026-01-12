namespace SpotifyTools.Web.DTOs;

public class ArtistDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Popularity { get; set; }
    public int Followers { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? ImageUrl { get; set; }
    public int SavedTrackCount { get; set; }
    public int PlaylistCount { get; set; }
}

public class ArtistDetailDto : ArtistDto
{
    public List<TrackDto> SavedTracks { get; set; } = new();
    public List<ArtistPlaylistDto> Playlists { get; set; } = new();
    public string? Description { get; set; } // Placeholder for future
}

public class ArtistPlaylistDto
{
    public string PlaylistId { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}
