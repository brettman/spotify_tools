using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyClientService;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Analytics;

public class AnalyticsService : IAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISpotifyClientService _spotifyClient;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IUnitOfWork unitOfWork,
        ISpotifyClientService spotifyClient,
        ILogger<AnalyticsService> logger)
    {
        _unitOfWork = unitOfWork;
        _spotifyClient = spotifyClient;
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

            // Fetch or retrieve audio analysis
            var audioAnalysis = await _unitOfWork.AudioAnalyses.GetByIdAsync(trackId);
            if (audioAnalysis == null && _spotifyClient.IsAuthenticated)
            {
                // Fetch from Spotify API and store
                try
                {
                    var spotifyAnalysis = await _spotifyClient.Client.Tracks.GetAudioAnalysis(trackId);

                    audioAnalysis = new AudioAnalysis
                    {
                        TrackId = trackId,
                        TrackTempo = spotifyAnalysis.Track.Tempo,
                        TrackKey = spotifyAnalysis.Track.Key,
                        TrackMode = spotifyAnalysis.Track.Mode,
                        TrackTimeSignature = spotifyAnalysis.Track.TimeSignature,
                        TrackLoudness = spotifyAnalysis.Track.Loudness,
                        Duration = spotifyAnalysis.Track.Duration,
                        FetchedAt = DateTime.UtcNow,
                        Sections = new List<AudioAnalysisSection>()
                    };

                    // Add sections
                    foreach (var section in spotifyAnalysis.Sections)
                    {
                        audioAnalysis.Sections.Add(new AudioAnalysisSection
                        {
                            TrackId = trackId,
                            Start = section.Start,
                            Duration = section.Duration,
                            Confidence = section.Confidence,
                            Loudness = section.Loudness,
                            Tempo = section.Tempo,
                            TempoConfidence = section.TempoConfidence,
                            Key = section.Key,
                            KeyConfidence = section.KeyConfidence,
                            Mode = section.Mode,
                            ModeConfidence = section.ModeConfidence,
                            TimeSignature = section.TimeSignature,
                            TimeSignatureConfidence = section.TimeSignatureConfidence
                        });
                    }

                    await _unitOfWork.AudioAnalyses.AddAsync(audioAnalysis);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Fetched and stored audio analysis for track {TrackId}", trackId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch audio analysis for track {TrackId}", trackId);
                }
            }

            if (audioAnalysis != null)
            {
                // Load sections if not already loaded
                var sections = (await _unitOfWork.AudioAnalysisSections.GetAllAsync())
                    .Where(s => s.TrackId == trackId)
                    .OrderBy(s => s.Start)
                    .ToList();

                report.AudioAnalysis = new TrackDetailReport.AudioAnalysisInfo
                {
                    TrackTempo = audioAnalysis.TrackTempo,
                    TrackKey = audioAnalysis.TrackKey,
                    TrackMode = audioAnalysis.TrackMode,
                    TrackTimeSignature = audioAnalysis.TrackTimeSignature,
                    Duration = audioAnalysis.Duration,
                    Sections = sections.Select(s => new TrackDetailReport.AudioAnalysisSection
                    {
                        Start = s.Start,
                        Duration = s.Duration,
                        Tempo = s.Tempo,
                        Key = s.Key,
                        Mode = s.Mode,
                        TimeSignature = s.TimeSignature,
                        Loudness = s.Loudness
                    }).ToList()
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

    public async Task<List<Artist>> GetAllArtistsSortedByPopularityAsync()
    {
        try
        {
            return (await _unitOfWork.Artists.GetAllAsync())
                .OrderByDescending(a => a.Followers)
                .ThenBy(a => a.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving artists sorted by popularity");
            throw;
        }
    }

    public async Task<List<Track>> GetTracksByArtistIdAsync(string artistId)
    {
        try
        {
            // Get all track-artist relationships for this artist
            var trackIds = (await _unitOfWork.TrackArtists.GetAllAsync())
                .Where(ta => ta.ArtistId == artistId)
                .Select(ta => ta.TrackId)
                .ToHashSet();

            // Get the actual tracks
            return (await _unitOfWork.Tracks.GetAllAsync())
                .Where(t => trackIds.Contains(t.Id))
                .OrderByDescending(t => t.Popularity)
                .ThenBy(t => t.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracks for artist {ArtistId}", artistId);
            throw;
        }
    }

    public async Task<List<Playlist>> GetAllPlaylistsSortedByNameAsync()
    {
        try
        {
            return (await _unitOfWork.Playlists.GetAllAsync())
                .OrderBy(p => p.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving playlists sorted by name");
            throw;
        }
    }

    public async Task<List<Track>> GetTracksByPlaylistIdAsync(string playlistId, bool preserveOrder = true)
    {
        try
        {
            // Get playlist tracks with their positions
            var playlistTracks = (await _unitOfWork.PlaylistTracks.GetAllAsync())
                .Where(pt => pt.PlaylistId == playlistId);

            if (preserveOrder)
            {
                playlistTracks = playlistTracks.OrderBy(pt => pt.Position);
            }

            var trackIds = playlistTracks.Select(pt => pt.TrackId).ToList();

            // Fetch tracks in order
            var tracks = new List<Track>();
            foreach (var trackId in trackIds)
            {
                var track = await _unitOfWork.Tracks.GetByIdAsync(trackId);
                if (track != null)
                {
                    tracks.Add(track);
                }
            }

            // If not preserving order, sort by popularity
            if (!preserveOrder)
            {
                tracks = tracks.OrderByDescending(t => t.Popularity).ThenBy(t => t.Name).ToList();
            }

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracks for playlist {PlaylistId}", playlistId);
            throw;
        }
    }

    public async Task<List<(string Genre, int ArtistCount)>> GetAllGenresAsync()
    {
        try
        {
            var allArtists = await _unitOfWork.Artists.GetAllAsync();

            // Flatten all genres and count artists per genre
            var genreCounts = allArtists
                .SelectMany(a => a.Genres.Select(g => (Artist: a, Genre: g)))
                .GroupBy(x => x.Genre, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Genre: g.Key, ArtistCount: g.DistinctBy(x => x.Artist.Id).Count()))
                .OrderBy(g => g.Genre)
                .ToList();

            return genreCounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all genres");
            throw;
        }
    }

    public async Task<List<Artist>> GetArtistsByGenreAsync(string genre)
    {
        try
        {
            var allArtists = await _unitOfWork.Artists.GetAllAsync();

            return allArtists
                .Where(a => a.Genres.Any(g => g.Equals(genre, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Followers)
                .ThenBy(a => a.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving artists for genre {Genre}", genre);
            throw;
        }
    }

    public async Task<GenreAnalysisReport> GetGenreAnalysisReportAsync()
    {
        try
        {
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            var report = new GenreAnalysisReport
            {
                TotalArtists = artists.Count(),
                TotalTracks = tracks.Count()
            };

            // Get all genres with artist counts
            var genreArtistMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var genreTrackMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var artist in artists)
            {
                foreach (var genre in artist.Genres)
                {
                    if (!genreArtistMap.ContainsKey(genre))
                    {
                        genreArtistMap[genre] = new HashSet<string>();
                        genreTrackMap[genre] = new HashSet<string>();
                    }
                    genreArtistMap[genre].Add(artist.Id);

                    // Add all tracks by this artist to this genre
                    var artistTrackIds = trackArtists
                        .Where(ta => ta.ArtistId == artist.Id)
                        .Select(ta => ta.TrackId);

                    foreach (var trackId in artistTrackIds)
                    {
                        genreTrackMap[genre].Add(trackId);
                    }
                }
            }

            report.TotalGenres = genreArtistMap.Count;

            // Calculate average genres per artist
            var artistsWithGenres = artists.Where(a => a.Genres.Length > 0).ToList();
            report.AverageGenresPerArtist = artistsWithGenres.Any()
                ? artistsWithGenres.Average(a => a.Genres.Length)
                : 0;

            // Build genre stats sorted by track count
            report.GenresByTrackCount = genreTrackMap
                .Select(kvp => new GenreAnalysisReport.GenreStats
                {
                    GenreName = kvp.Key,
                    ArtistCount = genreArtistMap[kvp.Key].Count,
                    TrackCount = kvp.Value.Count,
                    PercentageOfLibrary = (kvp.Value.Count / (double)report.TotalTracks) * 100
                })
                .OrderByDescending(g => g.TrackCount)
                .ToList();

            // Genre count distribution (how many artists have 1 genre, 2 genres, etc.)
            report.GenreCountDistribution = artists
                .GroupBy(a => a.Genres.Length)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Find genre overlaps (genres that frequently appear together on same artists)
            var genrePairs = new Dictionary<string, int>();

            foreach (var artist in artists.Where(a => a.Genres.Length > 1))
            {
                var sortedGenres = artist.Genres.OrderBy(g => g).ToList();
                for (int i = 0; i < sortedGenres.Count; i++)
                {
                    for (int j = i + 1; j < sortedGenres.Count; j++)
                    {
                        var key = $"{sortedGenres[i]}|{sortedGenres[j]}";
                        genrePairs[key] = genrePairs.GetValueOrDefault(key) + 1;
                    }
                }
            }

            report.TopGenreOverlaps = genrePairs
                .OrderByDescending(kvp => kvp.Value)
                .Take(20)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split('|');
                    return new GenreAnalysisReport.GenreOverlap
                    {
                        Genre1 = parts[0],
                        Genre2 = parts[1],
                        OverlapCount = kvp.Value,
                        OverlapPercentage = (kvp.Value / (double)artistsWithGenres.Count) * 100
                    };
                })
                .ToList();

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating genre analysis report");
            throw;
        }
    }

    public async Task<Dictionary<string, List<Track>>> GetTracksByGenreAsync()
    {
        try
        {
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            var genreTracksMap = new Dictionary<string, List<Track>>(StringComparer.OrdinalIgnoreCase);

            foreach (var artist in artists)
            {
                // Get all tracks by this artist
                var artistTrackIds = trackArtists
                    .Where(ta => ta.ArtistId == artist.Id)
                    .Select(ta => ta.TrackId)
                    .ToHashSet();

                var artistTracks = tracks.Where(t => artistTrackIds.Contains(t.Id)).ToList();

                // Add these tracks to all genres this artist belongs to
                foreach (var genre in artist.Genres)
                {
                    if (!genreTracksMap.ContainsKey(genre))
                    {
                        genreTracksMap[genre] = new List<Track>();
                    }

                    genreTracksMap[genre].AddRange(artistTracks);
                }
            }

            // Remove duplicates and sort by added date (most recent first)
            foreach (var genre in genreTracksMap.Keys.ToList())
            {
                genreTracksMap[genre] = genreTracksMap[genre]
                    .DistinctBy(t => t.Id)
                    .OrderByDescending(t => t.AddedAt)
                    .ToList();
            }

            return genreTracksMap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracks by genre");
            throw;
        }
    }

    public async Task<List<string>> GetAvailableGenreSeedsAsync()
    {
        try
        {
            if (!_spotifyClient.IsAuthenticated)
            {
                _logger.LogWarning("Cannot fetch genre seeds - Spotify client not authenticated");
                return new List<string>();
            }

            // Try to fetch from Spotify API - the method name varies by library version
            try
            {
                var genreSeeds = await _spotifyClient.Client.Browse.GetRecommendationGenres();
                _logger.LogInformation("Fetched {Count} genre seeds from Spotify", genreSeeds.Genres.Count);
                return genreSeeds.Genres;
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning(apiEx, "Spotify API call failed, returning empty list");
                return new List<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch genre seeds from Spotify API");
            return new List<string>();
        }
    }

    public async Task<List<GenreCluster>> SuggestGenreClustersAsync(int minTracksPerCluster = 20)
    {
        try
        {
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            // Build genre-to-track mapping
            var genreTrackMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var genreArtistMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var artist in artists)
            {
                foreach (var genre in artist.Genres)
                {
                    if (!genreTrackMap.ContainsKey(genre))
                    {
                        genreTrackMap[genre] = new HashSet<string>();
                        genreArtistMap[genre] = new HashSet<string>();
                    }

                    genreArtistMap[genre].Add(artist.Id);

                    var artistTrackIds = trackArtists
                        .Where(ta => ta.ArtistId == artist.Id)
                        .Select(ta => ta.TrackId);

                    foreach (var trackId in artistTrackIds)
                    {
                        genreTrackMap[genre].Add(trackId);
                    }
                }
            }

            // Filter out genres with too few tracks
            var viableGenres = genreTrackMap
                .Where(kvp => kvp.Value.Count >= minTracksPerCluster)
                .OrderByDescending(kvp => kvp.Value.Count)
                .ToList();

            // Auto-generate clusters using common genre patterns
            var clusters = new List<GenreCluster>();
            var assignedGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Define common genre cluster patterns
            var clusterPatterns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rock & Alternative"] = new() { "rock", "alternative", "indie rock", "garage rock", "psychedelic rock", "classic rock", "hard rock", "punk", "grunge" },
                ["Pop & Dance"] = new() { "pop", "dance pop", "electropop", "synth-pop", "indie pop", "art pop", "chamber pop" },
                ["Electronic & EDM"] = new() { "electronic", "edm", "house", "techno", "trance", "dubstep", "drum and bass", "ambient", "idm" },
                ["Hip Hop & Rap"] = new() { "hip hop", "rap", "trap", "boom bap", "conscious hip hop", "gangster rap", "southern hip hop" },
                ["R&B & Soul"] = new() { "r&b", "soul", "neo soul", "funk", "disco", "motown" },
                ["Metal & Heavy"] = new() { "metal", "heavy metal", "death metal", "black metal", "thrash metal", "doom metal", "metalcore" },
                ["Jazz & Blues"] = new() { "jazz", "blues", "bebop", "cool jazz", "fusion", "swing", "ragtime" },
                ["Folk & Acoustic"] = new() { "folk", "acoustic", "singer-songwriter", "americana", "bluegrass", "country" },
                ["Classical & Orchestral"] = new() { "classical", "orchestral", "opera", "baroque", "romantic", "contemporary classical" },
                ["Latin & World"] = new() { "latin", "salsa", "reggaeton", "bossa nova", "samba", "world", "afrobeat" }
            };

            // Try to match genres to cluster patterns
            foreach (var pattern in clusterPatterns)
            {
                var matchingGenres = viableGenres
                    .Where(kvp => pattern.Value.Any(keyword =>
                        kvp.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    .Where(kvp => !assignedGenres.Contains(kvp.Key))
                    .ToList();

                if (matchingGenres.Any())
                {
                    var allTrackIds = new HashSet<string>();
                    var allArtistIds = new HashSet<string>();
                    var clusterGenres = new List<string>();

                    foreach (var genreKvp in matchingGenres)
                    {
                        clusterGenres.Add(genreKvp.Key);
                        assignedGenres.Add(genreKvp.Key);

                        foreach (var trackId in genreKvp.Value)
                        {
                            allTrackIds.Add(trackId);
                        }

                        foreach (var artistId in genreArtistMap[genreKvp.Key])
                        {
                            allArtistIds.Add(artistId);
                        }
                    }

                    if (allTrackIds.Count >= minTracksPerCluster)
                    {
                        var primaryGenre = matchingGenres.OrderByDescending(g => g.Value.Count).First().Key;

                        clusters.Add(new GenreCluster
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = pattern.Key,
                            Description = $"Includes: {string.Join(", ", clusterGenres.Take(5))}{(clusterGenres.Count > 5 ? $" (+{clusterGenres.Count - 5} more)" : "")}",
                            Genres = clusterGenres.OrderByDescending(g => genreTrackMap[g].Count).ToList(),
                            PrimaryGenre = primaryGenre,
                            TotalTracks = allTrackIds.Count,
                            TotalArtists = allArtistIds.Count,
                            PercentageOfLibrary = (allTrackIds.Count / (double)tracks.Count()) * 100,
                            IsAutoGenerated = true
                        });
                    }
                }
            }

            // Create individual clusters for remaining large genres
            var remainingGenres = viableGenres
                .Where(kvp => !assignedGenres.Contains(kvp.Key))
                .Where(kvp => kvp.Value.Count >= minTracksPerCluster * 2) // Only if significantly large
                .Take(10) // Limit to avoid too many small clusters
                .ToList();

            foreach (var genreKvp in remainingGenres)
            {
                clusters.Add(new GenreCluster
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = CapitalizeGenre(genreKvp.Key),
                    Description = $"Focused on {genreKvp.Key} artists",
                    Genres = new List<string> { genreKvp.Key },
                    PrimaryGenre = genreKvp.Key,
                    TotalTracks = genreKvp.Value.Count,
                    TotalArtists = genreArtistMap[genreKvp.Key].Count,
                    PercentageOfLibrary = (genreKvp.Value.Count / (double)tracks.Count()) * 100,
                    IsAutoGenerated = true
                });
            }

            // Sort by track count descending
            return clusters.OrderByDescending(c => c.TotalTracks).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting genre clusters");
            throw;
        }
    }

    public async Task<ClusterPlaylistReport> GetClusterPlaylistReportAsync(GenreCluster cluster)
    {
        try
        {
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();
            var trackAlbums = await _unitOfWork.TrackAlbums.GetAllAsync();
            var albums = await _unitOfWork.Albums.GetAllAsync();

            var report = new ClusterPlaylistReport
            {
                Cluster = cluster
            };

            // Find all artists that match the cluster's genres
            var matchingArtists = artists
                .Where(a => a.Genres.Any(g => cluster.Genres.Contains(g, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            // Get all tracks by these artists
            var matchingTrackIds = new HashSet<string>();
            foreach (var artist in matchingArtists)
            {
                var artistTracks = trackArtists
                    .Where(ta => ta.ArtistId == artist.Id)
                    .Select(ta => ta.TrackId);

                foreach (var trackId in artistTracks)
                {
                    matchingTrackIds.Add(trackId);
                }
            }

            var matchingTracks = tracks.Where(t => matchingTrackIds.Contains(t.Id)).ToList();

            // Filter out excluded tracks if this is a saved cluster
            HashSet<string> excludedTrackIds = new();
            if (!string.IsNullOrEmpty(cluster.Id) && int.TryParse(cluster.Id, out int clusterId))
            {
                excludedTrackIds = await _unitOfWork.TrackExclusions.GetExcludedTrackIdsAsync(clusterId);
                matchingTracks = matchingTracks.Where(t => !excludedTrackIds.Contains(t.Id)).ToList();
            }

            // Build track info list
            foreach (var track in matchingTracks)
            {
                // Get primary artist
                var primaryTrackArtist = trackArtists
                    .Where(ta => ta.TrackId == track.Id)
                    .OrderBy(ta => ta.Position)
                    .FirstOrDefault();

                var artist = primaryTrackArtist != null
                    ? artists.FirstOrDefault(a => a.Id == primaryTrackArtist.ArtistId)
                    : null;

                // Get album
                var trackAlbum = trackAlbums.FirstOrDefault(ta => ta.TrackId == track.Id);
                var album = trackAlbum != null
                    ? albums.FirstOrDefault(a => a.Id == trackAlbum.AlbumId)
                    : null;

                // Find which genres from the cluster this track matches
                var matchedGenres = artist?.Genres
                    .Where(g => cluster.Genres.Contains(g, StringComparer.OrdinalIgnoreCase))
                    .ToList() ?? new List<string>();

                var duration = TimeSpan.FromMilliseconds(track.DurationMs);
                var formattedDuration = duration.TotalHours >= 1
                    ? $"{duration.Hours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                    : $"{duration.Minutes}:{duration.Seconds:D2}";

                report.Tracks.Add(new ClusterPlaylistReport.TrackInfo
                {
                    TrackId = track.Id,
                    TrackName = track.Name,
                    ArtistName = artist?.Name ?? "Unknown Artist",
                    AlbumName = album?.Name,
                    DurationMs = track.DurationMs,
                    FormattedDuration = formattedDuration,
                    Genres = artist?.Genres.ToList() ?? new List<string>(),
                    Popularity = track.Popularity,
                    AddedAt = track.AddedAt,
                    MatchedGenres = matchedGenres
                });
            }

            // Sort by added date (most recent first)
            report.Tracks = report.Tracks.OrderByDescending(t => t.AddedAt).ToList();

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cluster playlist report for {ClusterName}", cluster.Name);
            throw;
        }
    }

    private static string CapitalizeGenre(string genre)
    {
        if (string.IsNullOrEmpty(genre)) return genre;

        var words = genre.Split(' ');
        return string.Join(" ", words.Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1) : w));
    }

    public async Task<int> SaveClusterAsync(GenreCluster cluster, string? customName = null)
    {
        try
        {
            var name = customName ?? cluster.Name;

            // Check if name already exists
            if (await _unitOfWork.SavedClusters.ExistsByNameAsync(name))
            {
                _logger.LogWarning("Cluster with name '{Name}' already exists", name);
                throw new InvalidOperationException($"A cluster with the name '{name}' already exists. Please choose a different name.");
            }

            var savedCluster = new SavedCluster
            {
                Name = name,
                Description = cluster.Description,
                PrimaryGenre = cluster.PrimaryGenre,
                IsAutoGenerated = cluster.IsAutoGenerated,
                CreatedAt = DateTime.UtcNow,
                IsFinalized = false
            };

            savedCluster.SetGenresList(cluster.Genres);

            await _unitOfWork.SavedClusters.AddAsync(savedCluster);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Saved cluster '{Name}' with {GenreCount} genres and ID {Id}",
                name, cluster.Genres.Count, savedCluster.Id);

            return savedCluster.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cluster '{Name}'", customName ?? cluster.Name);
            throw;
        }
    }

    public async Task<List<GenreCluster>> GetSavedClustersAsync()
    {
        try
        {
            var savedClusters = await _unitOfWork.SavedClusters.GetAllOrderedAsync();
            var clusters = new List<GenreCluster>();

            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            foreach (var saved in savedClusters)
            {
                var cluster = await ConvertToGenreClusterAsync(saved, tracks, artists, trackArtists);
                clusters.Add(cluster);
            }

            return clusters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved clusters");
            throw;
        }
    }

    public async Task<GenreCluster?> GetSavedClusterByIdAsync(int id)
    {
        try
        {
            var saved = await _unitOfWork.SavedClusters.GetByIdAsync(id);
            if (saved == null)
            {
                return null;
            }

            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            return await ConvertToGenreClusterAsync(saved, tracks, artists, trackArtists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved cluster {Id}", id);
            throw;
        }
    }

    public async Task<bool> UpdateClusterAsync(int id, GenreCluster cluster)
    {
        try
        {
            var existing = await _unitOfWork.SavedClusters.GetByIdAsync(id);
            if (existing == null)
            {
                _logger.LogWarning("Cluster {Id} not found for update", id);
                return false;
            }

            // Check if renaming to an existing name
            if (existing.Name != cluster.Name &&
                await _unitOfWork.SavedClusters.ExistsByNameAsync(cluster.Name))
            {
                _logger.LogWarning("Cannot rename to '{Name}' - name already exists", cluster.Name);
                throw new InvalidOperationException($"A cluster with the name '{cluster.Name}' already exists.");
            }

            existing.Name = cluster.Name;
            existing.Description = cluster.Description;
            existing.PrimaryGenre = cluster.PrimaryGenre;
            existing.SetGenresList(cluster.Genres);
            existing.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.SavedClusters.Update(existing);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated cluster {Id} ('{Name}')", id, cluster.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cluster {Id}", id);
            throw;
        }
    }

    public async Task<bool> DeleteClusterAsync(int id)
    {
        try
        {
            var existing = await _unitOfWork.SavedClusters.GetByIdAsync(id);
            if (existing == null)
            {
                _logger.LogWarning("Cluster {Id} not found for deletion", id);
                return false;
            }

            _unitOfWork.SavedClusters.Delete(existing);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted cluster {Id} ('{Name}')", id, existing.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cluster {Id}", id);
            throw;
        }
    }

    public async Task<bool> FinalizeClusterAsync(int id)
    {
        try
        {
            var existing = await _unitOfWork.SavedClusters.GetByIdAsync(id);
            if (existing == null)
            {
                _logger.LogWarning("Cluster {Id} not found for finalization", id);
                return false;
            }

            existing.IsFinalized = true;
            existing.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.SavedClusters.Update(existing);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Finalized cluster {Id} ('{Name}')", id, existing.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing cluster {Id}", id);
            throw;
        }
    }

    public async Task ExcludeTrackAsync(int clusterId, string trackId)
    {
        try
        {
            await _unitOfWork.TrackExclusions.AddExclusionAsync(clusterId, trackId);
            _logger.LogInformation("Excluded track {TrackId} from cluster {ClusterId}", trackId, clusterId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error excluding track {TrackId} from cluster {ClusterId}", trackId, clusterId);
            throw;
        }
    }

    public async Task IncludeTrackAsync(int clusterId, string trackId)
    {
        try
        {
            await _unitOfWork.TrackExclusions.RemoveExclusionAsync(clusterId, trackId);
            _logger.LogInformation("Included track {TrackId} back into cluster {ClusterId}", trackId, clusterId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error including track {TrackId} into cluster {ClusterId}", trackId, clusterId);
            throw;
        }
    }

    public async Task<HashSet<string>> GetExcludedTrackIdsAsync(int clusterId)
    {
        try
        {
            return await _unitOfWork.TrackExclusions.GetExcludedTrackIdsAsync(clusterId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting excluded track IDs for cluster {ClusterId}", clusterId);
            throw;
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _unitOfWork.SaveChangesAsync();
    }

    public async Task<string> CreatePlaylistFromClusterAsync(int clusterId, bool makePublic = false)
    {
        try
        {
            // Load the cluster
            var savedCluster = await _unitOfWork.SavedClusters.GetByIdAsync(clusterId);
            if (savedCluster == null)
            {
                throw new InvalidOperationException($"Cluster {clusterId} not found.");
            }

            // Check if playlist already exists
            if (!string.IsNullOrEmpty(savedCluster.SpotifyPlaylistId))
            {
                _logger.LogWarning("Cluster {ClusterId} already has a playlist: {PlaylistId}",
                    clusterId, savedCluster.SpotifyPlaylistId);
                return savedCluster.SpotifyPlaylistId;
            }

            // Ensure client is authenticated
            if (!_spotifyClient.IsAuthenticated)
            {
                _logger.LogInformation("Spotify client not authenticated. Initiating authentication...");
                await _spotifyClient.AuthenticateAsync();
            }

            if (_spotifyClient.Client == null)
            {
                throw new InvalidOperationException("Spotify client failed to initialize.");
            }

            if (string.IsNullOrEmpty(_spotifyClient.UserId))
            {
                throw new InvalidOperationException("User ID is not available after authentication.");
            }

            // Get the cluster with track info
            var tracks = await _unitOfWork.Tracks.GetAllAsync();
            var artists = await _unitOfWork.Artists.GetAllAsync();
            var trackArtists = await _unitOfWork.TrackArtists.GetAllAsync();

            var genreCluster = await ConvertToGenreClusterAsync(savedCluster, tracks, artists, trackArtists);
            var report = await GetClusterPlaylistReportAsync(genreCluster);

            if (!report.Tracks.Any())
            {
                throw new InvalidOperationException($"Cluster '{savedCluster.Name}' has no tracks.");
            }

            _logger.LogInformation("Creating Spotify playlist for cluster '{ClusterName}' with {TrackCount} tracks",
                savedCluster.Name, report.Tracks.Count);

            // Create the playlist
            var playlistRequest = new SpotifyAPI.Web.PlaylistCreateRequest(savedCluster.Name)
            {
                Public = makePublic,
                Description = savedCluster.Description ?? $"Generated from genre cluster: {string.Join(", ", savedCluster.GetGenresList().Take(5))}"
            };

            var playlist = await _spotifyClient.Client.Playlists.Create(_spotifyClient.UserId, playlistRequest);

            _logger.LogInformation("Created Spotify playlist: {PlaylistId} ({PlaylistName})",
                playlist.Id, playlist.Name);

            // Add tracks to playlist (Spotify API limit is 100 tracks per request)
            var trackUris = report.Tracks
                .Select(t => $"spotify:track:{t.TrackId}")
                .ToList();

            const int batchSize = 100;
            for (int i = 0; i < trackUris.Count; i += batchSize)
            {
                var batch = trackUris.Skip(i).Take(batchSize).ToList();
                var addRequest = new SpotifyAPI.Web.PlaylistAddItemsRequest(batch);

                await _spotifyClient.Client.Playlists.AddItems(playlist.Id!, addRequest);

                _logger.LogInformation("Added {Count} tracks to playlist (batch {BatchNum}/{TotalBatches})",
                    batch.Count, (i / batchSize) + 1, (int)Math.Ceiling(trackUris.Count / (double)batchSize));
            }

            // Update the saved cluster with playlist info
            savedCluster.SpotifyPlaylistId = playlist.Id;
            savedCluster.PlaylistCreatedAt = DateTime.UtcNow;
            savedCluster.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.SavedClusters.Update(savedCluster);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully created playlist '{PlaylistName}' with {TrackCount} tracks",
                playlist.Name, trackUris.Count);

            return playlist.Id!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist from cluster {ClusterId}", clusterId);
            throw;
        }
    }

    private Task<GenreCluster> ConvertToGenreClusterAsync(
        SavedCluster saved,
        IEnumerable<Track> tracks,
        IEnumerable<Artist> artists,
        IEnumerable<TrackArtist> trackArtists)
    {
        var genres = saved.GetGenresList();

        // Calculate track and artist counts for this cluster
        var matchingArtists = artists
            .Where(a => a.Genres.Any(g => genres.Contains(g, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var matchingTrackIds = new HashSet<string>();
        foreach (var artist in matchingArtists)
        {
            var artistTracks = trackArtists
                .Where(ta => ta.ArtistId == artist.Id)
                .Select(ta => ta.TrackId);

            foreach (var trackId in artistTracks)
            {
                matchingTrackIds.Add(trackId);
            }
        }

        var totalTracks = tracks.Count();

        return Task.FromResult(new GenreCluster
        {
            Id = saved.Id.ToString(),
            Name = saved.Name,
            Description = saved.Description,
            Genres = genres,
            PrimaryGenre = saved.PrimaryGenre,
            TotalTracks = matchingTrackIds.Count,
            TotalArtists = matchingArtists.Count,
            PercentageOfLibrary = totalTracks > 0 ? (matchingTrackIds.Count / (double)totalTracks) * 100 : 0,
            IsAutoGenerated = saved.IsAutoGenerated
        });
    }
}
