# Spotify Genre Organizer

A C# console application that organizes your Spotify saved tracks into genre-specific playlists automatically.

## Features

- OAuth authentication with Spotify
- Fetches all your saved/favorite tracks
- Analyzes genre information from track artists
- Creates playlists based on configurable genre filters
- Supports multi-genre tracks (can add to multiple playlists or primary genre only)
- Handles thousands of tracks efficiently

## Prerequisites

1. **.NET SDK** (version 6.0 or higher)
   - Download from https://dotnet.microsoft.com/download

2. **Spotify Developer Account & App**
   - Go to https://developer.spotify.com/dashboard
   - Log in with your Spotify account
   - Click "Create an App"
   - Give it a name (e.g., "Genre Organizer")
   - Note your **Client ID** and **Client Secret**
   - Click "Edit Settings" and add `http://localhost:5000/callback` to the Redirect URIs
   - Save the settings

## Setup Instructions

### 1. Run the Setup Script

```bash
./setup-spotify-app.sh
```

This will:
- Create the solution and project structure
- Install all required NuGet packages

### 2. Configure the Application

Copy the template configuration file:

```bash
cp appsettings.json.template SpotifyGenreOrganizer/src/SpotifyGenreOrganizer/appsettings.json
```

Edit `appsettings.json` and add your Spotify credentials:

```json
{
  "Spotify": {
    "ClientId": "your_actual_client_id",
    "ClientSecret": "your_actual_client_secret",
    "RedirectUri": "http://localhost:5000/callback"
  },
  "GenreFilters": [
    "rock",
    "pop",
    "jazz"
  ],
  "MultiGenreBehavior": "AddToAll"
}
```

**Configuration Options:**

- `GenreFilters`: Array of genre names you want to create playlists for
- `MultiGenreBehavior`:
  - `"AddToAll"` - Add tracks to all matching genre playlists
  - `"PrimaryOnly"` - Add tracks only to their primary genre

### 3. Copy Program.cs

```bash
cp Program.cs.template SpotifyGenreOrganizer/src/SpotifyGenreOrganizer/Program.cs
```

### 4. (Optional) Setup Git Ignore

If you're using git:

```bash
cp .gitignore.template SpotifyGenreOrganizer/.gitignore
```

This ensures your Spotify credentials don't get committed to version control.

## Running the Application

Navigate to the project directory and run:

```bash
cd SpotifyGenreOrganizer
dotnet run --project src/SpotifyGenreOrganizer
```

The application will:
1. Open your browser for Spotify authorization
2. Fetch all your saved tracks
3. Analyze genres for each track
4. Show you genre statistics
5. Create playlists for your configured genres

## How It Works

### Genre Detection

The app fetches genre information from each track's artists. Since Spotify doesn't assign genres directly to tracks, the app:
- Looks at all artists on each track
- Retrieves their genre tags
- Assigns tracks to genre categories based on these tags

### Genre Matching

The genre matching is flexible:
- Case-insensitive matching
- Partial matching (e.g., "rock" will match "alternative rock", "indie rock", etc.)
- Configurable target genres in `appsettings.json`

### Rate Limiting

The app includes small delays between API calls to respect Spotify's rate limits.

## Customization

### Adding More Genres

Edit the `GenreFilters` array in `appsettings.json`:

```json
"GenreFilters": [
  "rock",
  "pop",
  "hip hop",
  "electronic",
  "jazz",
  "classical",
  "indie",
  "metal",
  "country",
  "r&b",
  "soul",
  "blues",
  "reggae",
  "folk"
]
```

### Playlist Names

Playlists are named: `[GENRE] - Auto Generated`

You can modify the naming in `Program.cs` line 265:

```csharp
var playlistName = $"{targetGenre.ToUpper()} - Auto Generated";
```

### Public vs Private Playlists

By default, playlists are created as private. To make them public, change line 268:

```csharp
Public = true
```

## Troubleshooting

### "Failed to authenticate"
- Verify your Client ID and Client Secret are correct
- Make sure the Redirect URI in your Spotify app settings matches exactly: `http://localhost:5000/callback`

### "Port 5000 already in use"
- Change the port in `appsettings.json` and in your Spotify app's Redirect URIs
- Make sure both match exactly

### "No tracks found for genre"
- The genre might not exist in your library
- Try using more general genre names (e.g., "rock" instead of "progressive rock")
- Check the genre statistics output to see what genres are available

### Rate Limiting
- The app includes delays to avoid rate limiting
- If you hit rate limits, the delays might need to be increased in the code

## Dependencies

- **SpotifyAPI.Web** - Spotify Web API wrapper
- **SpotifyAPI.Web.Auth** - OAuth authentication for Spotify
- **Microsoft.Extensions.Configuration** - Configuration management
- **Newtonsoft.Json** - JSON serialization

## License

MIT License - Feel free to modify and use as needed!

## Tips

- Start with a smaller set of genres to test
- Review the genre statistics before creating playlists
- The app creates new playlists each time - it doesn't update existing ones
- Consider running periodically to catch new saved tracks
