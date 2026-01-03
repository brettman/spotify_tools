using System.ComponentModel.DataAnnotations;

namespace SpotifyTools.Web.DTOs;

public class CreatePlaylistRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public bool IsPublic { get; set; }
}

public class AddTracksRequest
{
    [Required]
    [MinLength(1)]
    public List<string> TrackIds { get; set; } = new();
}
