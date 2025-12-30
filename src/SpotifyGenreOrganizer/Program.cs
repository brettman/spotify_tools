using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpotifyAPI.Web;
using SpotifyClientService;

namespace SpotifyGenreOrganizer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Spotify Genre Organizer ===\n");

            // Build host with dependency injection
            var host = CreateHostBuilder(args).Build();

            // Get services
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var spotifyService = host.Services.GetRequiredService<ISpotifyClientService>();

            // Authenticate with Spotify
            await spotifyService.AuthenticateAsync();

            // Get user profile info
            var profile = await spotifyService.Client.UserProfile.Current();
            Console.WriteLine($"Logged in as: {profile.DisplayName}\n");

            // Fetch all saved tracks
            Console.WriteLine("Fetching your saved tracks...");
            var savedTracks = await FetchAllSavedTracksAsync(spotifyService.Client);
            Console.WriteLine($"Found {savedTracks.Count} saved tracks\n");

            // Analyze genres
            Console.WriteLine("Analyzing genres...");
            var tracksByGenre = await CategorizeTracksByGenreAsync(
                spotifyService.Client,
                savedTracks,
                configuration);

            // Display genre statistics
            Console.WriteLine("\nGenre Distribution:");
            foreach (var genre in tracksByGenre.OrderByDescending(g => g.Value.Count))
            {
                Console.WriteLine($"  {genre.Key}: {genre.Value.Count} tracks");
            }

            // Get target genres from configuration
            var targetGenres = configuration.GetSection("GenreFilters").Get<List<string>>()
                ?? new List<string>();

            if (!targetGenres.Any())
            {
                Console.WriteLine("\nNo genres specified in appsettings.json!");
                Console.WriteLine("Please add genres to the 'GenreFilters' section.");
                return;
            }

            Console.WriteLine($"\nTarget genres: {string.Join(", ", targetGenres)}");
            Console.Write("\nProceed with playlist creation? (y/n): ");
            var response = Console.ReadLine()?.ToLower();

            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Cancelled.");
                return;
            }

            // Create playlists for each genre
            await CreateGenrePlaylistsAsync(
                spotifyService.Client,
                spotifyService.UserId!,
                tracksByGenre,
                targetGenres);

            Console.WriteLine("\n✓ Complete! Your genre playlists have been created.");
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ISpotifyClientService, SpotifyClientWrapper>();
                });

        static async Task<List<SavedTrack>> FetchAllSavedTracksAsync(SpotifyClient spotify)
        {
            var allTracks = new List<SavedTrack>();
            var offset = 0;
            const int limit = 50;

            while (true)
            {
                var tracks = await spotify.Library.GetTracks(new LibraryTracksRequest
                {
                    Limit = limit,
                    Offset = offset
                });

                if (tracks.Items == null || !tracks.Items.Any())
                    break;

                allTracks.AddRange(tracks.Items!);
                Console.Write($"\rFetched {allTracks.Count} tracks...");

                if (tracks.Items.Count < limit)
                    break;

                offset += limit;
            }

            Console.WriteLine();
            return allTracks;
        }

        static async Task<Dictionary<string, List<FullTrack>>> CategorizeTracksByGenreAsync(
            SpotifyClient spotify,
            List<SavedTrack> savedTracks,
            IConfiguration configuration)
        {
            var tracksByGenre = new Dictionary<string, List<FullTrack>>(StringComparer.OrdinalIgnoreCase);
            var processedCount = 0;
            var multiGenreBehavior = configuration["MultiGenreBehavior"] ?? "AddToAll";

            foreach (var savedTrack in savedTracks)
            {
                processedCount++;
                if (processedCount % 50 == 0)
                {
                    Console.Write($"\rAnalyzing track {processedCount}/{savedTracks.Count}...");
                }

                var track = savedTrack.Track;
                var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Get genres from all artists on the track
                foreach (var artist in track.Artists)
                {
                    try
                    {
                        var fullArtist = await spotify.Artists.Get(artist.Id);
                        if (fullArtist.Genres != null)
                        {
                            foreach (var genre in fullArtist.Genres)
                            {
                                genres.Add(genre);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nWarning: Could not fetch artist {artist.Name}: {ex.Message}");
                    }

                    // Add small delay to avoid rate limiting
                    await Task.Delay(50);
                }

                // If no genres found, categorize as "Unknown"
                if (!genres.Any())
                {
                    genres.Add("Unknown");
                }

                // Add track to genre categories
                if (multiGenreBehavior == "PrimaryOnly" && genres.Any())
                {
                    var primaryGenre = genres.First();
                    if (!tracksByGenre.ContainsKey(primaryGenre))
                        tracksByGenre[primaryGenre] = new List<FullTrack>();
                    tracksByGenre[primaryGenre].Add(track);
                }
                else // AddToAll
                {
                    foreach (var genre in genres)
                    {
                        if (!tracksByGenre.ContainsKey(genre))
                            tracksByGenre[genre] = new List<FullTrack>();
                        tracksByGenre[genre].Add(track);
                    }
                }
            }

            Console.WriteLine($"\rAnalyzing track {processedCount}/{savedTracks.Count}... Done!");
            return tracksByGenre;
        }

        static async Task CreateGenrePlaylistsAsync(
            SpotifyClient spotify,
            string userId,
            Dictionary<string, List<FullTrack>> tracksByGenre,
            List<string> targetGenres)
        {
            foreach (var targetGenre in targetGenres)
            {
                // Find matching genre (case-insensitive, partial match)
                var matchingGenres = tracksByGenre.Keys
                    .Where(g => g.Contains(targetGenre, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!matchingGenres.Any())
                {
                    Console.WriteLine($"\n⚠ No tracks found for genre: {targetGenre}");
                    continue;
                }

                // Combine all matching genre tracks
                var tracks = matchingGenres
                    .SelectMany(g => tracksByGenre[g])
                    .DistinctBy(t => t.Id)
                    .ToList();

                Console.WriteLine($"\nCreating playlist for '{targetGenre}' ({tracks.Count} tracks)...");

                // Create playlist
                var playlistName = $"{targetGenre.ToUpper()} - Auto Generated";
                var playlist = await spotify.Playlists.Create(
                    userId,
                    new PlaylistCreateRequest(playlistName)
                    {
                        Description = $"Auto-generated playlist containing {targetGenre} tracks from your saved library.",
                        Public = false
                    }
                );

                Console.WriteLine($"✓ Created playlist: {playlistName}");

                // Add tracks to playlist (Spotify allows max 100 tracks per request)
                var trackUris = tracks.Select(t => t.Uri).ToList();
                for (int i = 0; i < trackUris.Count; i += 100)
                {
                    var batch = trackUris.Skip(i).Take(100).ToList();
                    await spotify.Playlists.AddItems(
                        playlist.Id!,
                        new PlaylistAddItemsRequest(batch)
                    );
                    Console.WriteLine($"  Added {Math.Min(i + 100, trackUris.Count)}/{trackUris.Count} tracks");
                }

                Console.WriteLine($"✓ Completed playlist: {playlistName}");
            }
        }
    }
}
