# Spotify Tools

A C# application that syncs your Spotify library to PostgreSQL for offline access and custom analytics, with a focus on audio features (tempo, key, energy, etc.) for DJ mixing and music analysis.

## Features

### Current
- ✅ **Full Library Sync** - Import all saved tracks, artists, albums, playlists
- ✅ **Audio Features** - Tempo, key, mode, danceability, energy, and more
- ✅ **PostgreSQL Storage** - Local database for offline queries and analytics
- ✅ **Interactive CLI** - User-friendly menu interface
- ✅ **Sync History** - Track all import operations with statistics
- ✅ **Rate Limiting** - Respects Spotify API limits (60 requests/min)

### Coming Soon
- ⏳ **Analytics & Reports** - Tempo distribution, key analysis, genre statistics
- 📅 **Incremental Sync** - Update only changed data
- 📅 **Web Interface** - Browse and analyze your library in a browser

## Architecture

```
SpotifyTools/
├── SpotifyTools.Domain      # Entity models (Track, Artist, Album, etc.)
├── SpotifyTools.Data         # EF Core, repositories, Unit of Work
├── SpotifyTools.Sync         # Sync orchestration with rate limiting
├── SpotifyTools.Analytics    # Analytics and reporting (coming soon)
├── SpotifyClientService      # Spotify API wrapper with OAuth
└── SpotifyGenreOrganizer     # CLI interface
```

## Prerequisites

1. **.NET 8.0 SDK**
   - Download from https://dotnet.microsoft.com/download

2. **Docker Desktop**
   - For running PostgreSQL database
   - Download from https://www.docker.com/products/docker-desktop

3. **Spotify Developer App**
   - Go to https://developer.spotify.com/dashboard
   - Create a new app
   - Note your **Client ID** and **Client Secret**
   - Add redirect URI: `http://127.0.0.1:5009/callback`

## Quick Start

### 1. Clone and Setup

```bash
cd /path/to/spotify_tools
```

### 2. Start PostgreSQL Database

```bash
# Copy environment template
cp .env.template .env

# Edit .env and set a secure password
# Then start PostgreSQL
docker-compose up -d

# Verify it's running
docker-compose ps
```

### 3. Configure Application

```bash
# Copy the template and edit with your credentials
cd src/SpotifyGenreOrganizer
cp appsettings.json.template appsettings.json
```

Edit `appsettings.json` with your Spotify credentials and database password:

```json
{
  "ConnectionStrings": {
    "SpotifyDatabase": "Host=localhost;Port=5433;Database=spotify_tools;Username=spotify_user;Password=YOUR_PASSWORD"
  },
  "Spotify": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "RedirectUri": "http://127.0.0.1:5009/callback"
  }
}
```

### 4. Apply Database Migrations

```bash
cd src/SpotifyTools.Data
dotnet ef database update
```

### 5. Run the Application

```bash
cd ../SpotifyGenreOrganizer
dotnet run
```

## Using the CLI

### Main Menu

```
╔════════════════════════════════════════╗
║     Spotify Tools - CLI Interface     ║
╠════════════════════════════════════════╣
║  1. Full Sync (Import all data)       ║
║  2. View Last Sync Status              ║
║  3. View Sync History                  ║
║  4. Analytics (Coming soon)            ║
║  5. Exit                               ║
╚════════════════════════════════════════╝
```

### Full Sync

Option 1 performs a complete import:
1. Authenticates with Spotify (opens browser)
2. Fetches all saved tracks
3. Fetches artist details with genres
4. Fetches album details
5. Fetches audio features (tempo, key, etc.)
6. Fetches user playlists

**Time Estimate:**
- ~1 hour per 3,000 tracks (due to API rate limiting)
- Progress updates shown in real-time

### View Sync Status

Options 2 & 3 show:
- Last sync completion time
- Statistics (tracks, artists, albums processed)
- Success/failure status
- Historical sync data

## Database Schema

### Core Tables
- **tracks** - Track metadata (name, duration, popularity, ISRC)
- **artists** - Artist data (name, genres, popularity, followers)
- **albums** - Album information (name, release date, label)
- **audio_features** - Audio analysis (tempo, key, danceability, energy, etc.)
- **playlists** - User playlists

### Relationship Tables
- **track_artists** - Many-to-many with artist position tracking
- **track_albums** - Track-album relationships with disc/track numbers
- **playlist_tracks** - Playlist contents with positions

### Metadata Tables
- **sync_history** - Tracks all sync operations
- **spotify_tokens** - OAuth tokens (future use)

## Configuration

### Database (PostgreSQL)
- **Port:** 5433 (mapped from container)
- **Database:** spotify_tools
- **User:** spotify_user
- **Password:** Set in `.env` file

### Spotify API
- **Rate Limit:** 60 requests/minute (configurable in code)
- **Scopes:** UserLibraryRead, PlaylistModifyPublic, PlaylistModifyPrivate
- **OAuth:** Authorization code flow with browser

## Troubleshooting

### OAuth "INVALID_CLIENT" Error
- Ensure redirect URI in Spotify Dashboard exactly matches: `http://127.0.0.1:5009/callback`
- Use `127.0.0.1` not `localhost`
- Verify Client ID and Secret are correct

### Port Already in Use
- PostgreSQL runs on port 5433 to avoid conflicts with local installations
- Change port in `docker-compose.yml` and connection string if needed

### Foreign Key Errors
- Ensure database migrations are applied: `dotnet ef database update`
- Check PostgreSQL is running: `docker-compose ps`

### DateTime UTC Errors
- This is handled in the code
- All DateTimes are converted to UTC before saving

## Project Documentation

- **context.md** - Detailed project status, architecture decisions, and progress tracking
- **CLAUDE.md** - Instructions for Claude Code when working on this project
- **DOCKER.md** - Docker setup and PostgreSQL management guide

## Technology Stack

- **Language:** C# / .NET 8
- **Database:** PostgreSQL 16 (Docker)
- **ORM:** Entity Framework Core 8.0
- **Spotify API:** SpotifyAPI.Web 7.2.1
- **Architecture:** Clean Architecture with Repository pattern

## Roadmap

### Phase 1-5: Complete ✅
- Project structure and domain models
- Data layer with EF Core
- Sync service with rate limiting
- CLI interface
- Full library import

### Phase 6: In Progress ⏳
- Analytics service
- Tempo analysis and distribution
- Key/mode distribution for DJ mixing
- Genre statistics

### Future Phases
- Incremental sync (only fetch changes)
- Web interface (ASP.NET Core)
- Advanced analytics (correlations, recommendations)
- External data integration (MusicBrainz)
- Export and backup functionality

## Contributing

This is a personal project, but suggestions and feedback are welcome!

## License

MIT License - Feel free to use and modify for your own purposes.

## Acknowledgments

- Built with [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)
- Powered by [Entity Framework Core](https://github.com/dotnet/efcore)
- Database: [PostgreSQL](https://www.postgresql.org/)
