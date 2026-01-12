using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

public interface IArtistService
{
    Task<List<ArtistDto>> GetAllArtistsAsync();
    Task<ArtistDetailDto?> GetArtistByIdAsync(string artistId);
    Task<List<ArtistDto>> SearchArtistsAsync(string searchQuery);
}

public class ArtistService : IArtistService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly ILogger<ArtistService> _logger;

    public ArtistService(SpotifyDbContext dbContext, ILogger<ArtistService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<ArtistDto>> GetAllArtistsAsync()
    {
        try
        {
            var artists = await _dbContext.Artists
                .OrderBy(a => a.Name)
                .Select(a => new ArtistDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Popularity = a.Popularity,
                    Followers = a.Followers,
                    Genres = a.Genres.ToList(),
                    ImageUrl = a.ImageUrl,
                    SavedTrackCount = a.TrackArtists.Count(ta => ta.Track.AddedAt != null),
                    PlaylistCount = a.TrackArtists
                        .SelectMany(ta => ta.Track.PlaylistTracks)
                        .Select(pt => pt.PlaylistId)
                        .Distinct()
                        .Count()
                })
                .ToListAsync();

            return artists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artists");
            throw;
        }
    }

    public async Task<ArtistDetailDto?> GetArtistByIdAsync(string artistId)
    {
        try
        {
            var artist = await _dbContext.Artists
                .Where(a => a.Id == artistId)
                .Select(a => new ArtistDetailDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Popularity = a.Popularity,
                    Followers = a.Followers,
                    Genres = a.Genres.ToList(),
                    ImageUrl = a.ImageUrl,
                    SavedTracks = a.TrackArtists
                        .Where(ta => ta.Track.AddedAt != null)
                        .OrderBy(ta => ta.Track.Name)
                        .Select(ta => new TrackDto
                        {
                            Id = ta.Track.Id,
                            Name = ta.Track.Name,
                            AlbumName = ta.Track.TrackAlbums
                                .Select(tab => tab.Album.Name)
                                .FirstOrDefault(),
                            DurationMs = ta.Track.DurationMs,
                            Popularity = ta.Track.Popularity,
                            Explicit = ta.Track.Explicit,
                            AddedAt = ta.Track.AddedAt,
                            Artists = ta.Track.TrackArtists
                                .OrderBy(ta2 => ta2.Position)
                                .Select(ta2 => new ArtistSummaryDto
                                {
                                    Id = ta2.Artist.Id,
                                    Name = ta2.Artist.Name,
                                    Genres = ta2.Artist.Genres.ToList()
                                })
                                .ToList(),
                            Genres = ta.Track.TrackArtists
                                .SelectMany(ta2 => ta2.Artist.Genres)
                                .Distinct()
                                .ToList()
                        })
                        .ToList(),
                    Playlists = a.TrackArtists
                        .SelectMany(ta => ta.Track.PlaylistTracks)
                        .GroupBy(pt => new { pt.PlaylistId, pt.Playlist.Name })
                        .Select(g => new ArtistPlaylistDto
                        {
                            PlaylistId = g.Key.PlaylistId,
                            PlaylistName = g.Key.Name,
                            TrackCount = g.Count()
                        })
                        .OrderByDescending(p => p.TrackCount)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (artist != null)
            {
                artist.SavedTrackCount = artist.SavedTracks.Count;
                artist.PlaylistCount = artist.Playlists.Count;
            }

            return artist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artist {ArtistId}", artistId);
            throw;
        }
    }

    public async Task<List<ArtistDto>> SearchArtistsAsync(string searchQuery)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return await GetAllArtistsAsync();
            }

            var artists = await _dbContext.Artists
                .Where(a => EF.Functions.ILike(a.Name, $"%{searchQuery}%"))
                .OrderBy(a => a.Name)
                .Select(a => new ArtistDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Popularity = a.Popularity,
                    Followers = a.Followers,
                    Genres = a.Genres.ToList(),
                    ImageUrl = a.ImageUrl,
                    SavedTrackCount = a.TrackArtists.Count(ta => ta.Track.AddedAt != null),
                    PlaylistCount = a.TrackArtists
                        .SelectMany(ta => ta.Track.PlaylistTracks)
                        .Select(pt => pt.PlaylistId)
                        .Distinct()
                        .Count()
                })
                .ToListAsync();

            return artists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching artists with query: {Query}", searchQuery);
            throw;
        }
    }
}
