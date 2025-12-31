using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotifyTools.Data.Repositories.Interfaces;

namespace SpotifyTools.Analytics;

public class AnalyticsService : IAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(IUnitOfWork unitOfWork, ILogger<AnalyticsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TrackDetailReport?> GetTrackDetailReportAsync(string trackId)
    {
        try
        {
            // Fetch track
            var track = await _unitOfWork.Tracks.GetByIdAsync(trackId);
            if (track == null)
            {
                _logger.LogWarning("Track {TrackId} not found", trackId);
                return null;
            }

            var report = new TrackDetailReport
            {
                TrackId = track.Id,
                Name = track.Name,
                DurationMs = track.DurationMs,
                Explicit = track.Explicit,
                Popularity = track.Popularity,
                Isrc = track.Isrc,
                AddedAt = track.AddedAt
            };

            // Fetch artists
            var trackArtists = (await _unitOfWork.TrackArtists.GetAllAsync())
                .Where(ta => ta.TrackId == trackId)
                .OrderBy(ta => ta.Position)
                .ToList();

            foreach (var ta in trackArtists)
            {
                var artist = await _unitOfWork.Artists.GetByIdAsync(ta.ArtistId);
                if (artist != null)
                {
                    report.Artists.Add(new TrackDetailReport.ArtistInfo
                    {
                        Name = artist.Name,
                        Genres = artist.Genres.ToList(),
                        Popularity = artist.Popularity,
                        Followers = artist.Followers
                    });
                }
            }

            // Fetch album
            var trackAlbum = (await _unitOfWork.TrackAlbums.GetAllAsync())
                .FirstOrDefault(ta => ta.TrackId == trackId);

            if (trackAlbum != null)
            {
                var album = await _unitOfWork.Albums.GetByIdAsync(trackAlbum.AlbumId);
                if (album != null)
                {
                    report.Album = new TrackDetailReport.AlbumInfo
                    {
                        Name = album.Name,
                        AlbumType = album.AlbumType,
                        ReleaseDate = album.ReleaseDate,
                        Label = album.Label,
                        TotalTracks = album.TotalTracks
                    };
                }
            }

            // Fetch audio features
            var audioFeatures = await _unitOfWork.AudioFeatures.GetByIdAsync(trackId);
            if (audioFeatures != null)
            {
                report.AudioFeatures = new TrackDetailReport.AudioFeaturesInfo
                {
                    Tempo = audioFeatures.Tempo,
                    Key = audioFeatures.Key,
                    Mode = audioFeatures.Mode,
                    TimeSignature = audioFeatures.TimeSignature,
                    Danceability = audioFeatures.Danceability,
                    Energy = audioFeatures.Energy,
                    Acousticness = audioFeatures.Acousticness,
                    Instrumentalness = audioFeatures.Instrumentalness,
                    Liveness = audioFeatures.Liveness,
                    Loudness = audioFeatures.Loudness,
                    Speechiness = audioFeatures.Speechiness,
                    Valence = audioFeatures.Valence
                };
            }

            // Fetch playlists containing this track
            var playlistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
                .Where(pt => pt.TrackId == trackId)
                .ToList();

            foreach (var pt in playlistTracks)
            {
                var playlist = await _unitOfWork.Playlists.GetByIdAsync(pt.PlaylistId);
                if (playlist != null)
                {
                    report.Playlists.Add(playlist.Name);
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating track detail report for {TrackId}", trackId);
            throw;
        }
    }

    public async Task<List<(string TrackId, string DisplayName)>> SearchTracksAsync(string searchTerm, int limit = 10)
    {
        try
        {
            var tracks = (await _unitOfWork.Tracks.GetAllAsync())
                .Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            var results = new List<(string TrackId, string DisplayName)>();

            foreach (var track in tracks)
            {
                // Get first artist for display
                var trackArtist = (await _unitOfWork.TrackArtists.GetAllAsync())
                    .Where(ta => ta.TrackId == track.Id)
                    .OrderBy(ta => ta.Position)
                    .FirstOrDefault();

                var artistName = "Unknown Artist";
                if (trackArtist != null)
                {
                    var artist = await _unitOfWork.Artists.GetByIdAsync(trackArtist.ArtistId);
                    if (artist != null)
                    {
                        artistName = artist.Name;
                    }
                }

                results.Add((track.Id, $"{track.Name} - {artistName}"));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tracks with term '{SearchTerm}'", searchTerm);
            throw;
        }
    }
}
