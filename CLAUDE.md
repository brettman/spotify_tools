# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Spotify Genre Organizer is a C# console application that organizes Spotify saved/favorite tracks into genre-specific playlists. The application uses OAuth authentication, fetches all saved tracks, analyzes artist genres (since Spotify doesn't assign genres to tracks), and creates playlists based on configurable genre filters.

## Building and Running

```bash
# Build the solution
dotnet build SpotifyGenreOrganizer.sln

# Run the application
dotnet run --project src/SpotifyGenreOrganizer

# Restore packages (if needed)
dotnet restore
```

## Configuration

The application is configured via `src/SpotifyGenreOrganizer/appsettings.json`:

- **Spotify Credentials**: `ClientId`, `ClientSecret`, and `RedirectUri` must match settings in the Spotify Developer Dashboard
- **Port Configuration**: Both `RedirectUri` in appsettings.json AND the port parameter in `EmbedIOAuthServer` constructor (Program.cs line 96) must match
- **GenreFilters**: Array of genre strings for playlist creation (supports partial matching, case-insensitive)
- **MultiGenreBehavior**: "AddToAll" adds tracks to all matching genre playlists, "PrimaryOnly" uses first genre only

## Architecture

### Single-File Console App (Program.cs)

The application uses a monolithic structure with all logic in `Program.cs`:

- **Configuration Loading**: Uses Microsoft.Extensions.Configuration with JSON file and environment variable support
- **OAuth Flow**: EmbedIOAuthServer creates local callback server for Spotify OAuth authorization code flow
- **Data Fetching**: Paginated fetching of saved tracks (50 per request) with pagination handling
- **Genre Analysis**:
  - Genres come from artist data, not tracks directly
  - Each track's artists are fetched individually to get genre tags
  - 50ms delay between artist API calls to avoid rate limiting
  - Tracks with no artist genres are categorized as "Unknown"
- **Playlist Creation**:
  - Genre matching uses partial string matching (e.g., "rock" matches "indie rock")
  - Playlists created as private by default
  - Batch adds tracks in groups of 100 (Spotify API limit)

### Key Dependencies

- **SpotifyAPI.Web**: Main Spotify API wrapper
- **SpotifyAPI.Web.Auth**: OAuth authentication with EmbedIOAuthServer
- **Microsoft.Extensions.Configuration**: Configuration management with Binder for typed access

## Important Implementation Details

### Port Synchronization
When changing the OAuth callback port, update BOTH locations:
1. `appsettings.json` RedirectUri
2. `EmbedIOAuthServer` constructor second parameter in Program.cs

### Rate Limiting
The app includes a 50ms delay between artist fetches (Program.cs in `CategorizeTracksByGenreAsync`). If rate limiting occurs, increase this delay.

### Spotify API Scopes
Required OAuth scopes (Program.cs line 117-121):
- `UserLibraryRead`: Access saved tracks
- `PlaylistModifyPublic`: Create/modify public playlists
- `PlaylistModifyPrivate`: Create/modify private playlists

### Configuration Security
`appsettings.json` contains sensitive credentials and should be in `.gitignore`. Template file is `appsettings.json.template`.
