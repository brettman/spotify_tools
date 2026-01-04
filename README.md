# Spotify Tools

A C# application that syncs your Spotify library to PostgreSQL for offline access and custom analytics. Analyze your music collection with genre statistics, artist insights, playlist analysis, and temporal trends.

## Features

### Current
- ✅ **Full Library Sync** - Import all saved tracks, artists, albums, playlists (including playlist-only tracks)
- ✅ **Incremental Sync** - Fast updates for new/changed data only (tracks, artists, albums, playlists)
- ✅ **Partial Sync** - Sync individual stages (tracks, artists, albums, playlists)
- ✅ **PostgreSQL Storage** - Local database with snake_case naming for offline queries
- ✅ **Interactive CLI** - Beautiful terminal interface powered by Spectre.Console
- ✅ **Track Navigation** - Browse by artist, playlist, genre, or search by name
- ✅ **Paginated Tables** - View large datasets with 30 rows per page navigation
- ✅ **Analytics Views** - 6 functional PostgreSQL views for data visualization
- ✅ **Sync History** - Track all import operations with statistics
- ✅ **Rate Limiting** - Respects Spotify API limits (30 requests/min)
- ✅ **Genre Analysis** - Comprehensive genre landscape with overlaps and statistics
- ✅ **Genre Clustering** - Auto-suggested clusters for playlist organization
- ✅ **Interactive Cluster Refinement** - Review and remove genres that don't fit
- ✅ **Cluster Persistence** - Save, edit, delete, and finalize refined clusters
- ✅ **Cluster Management** - Full CRUD operations with database persistence
- ✅ **Smart Genre Handling** - Orphaned genres intelligently reassigned or tracked
- ✅ **Playlist Generation** - Create Spotify playlists from finalized genre clusters
- ✅ **Track Exclusion System** - Remove specific tracks from cluster playlists
- ✅ **Artist Insights** - Top artists by follower count, track count, and popularity
- ❌ **Audio Features** - ~~Unavailable (Spotify API restricted as of Nov 27, 2024)~~

### Coming Soon
- 📅 **Track Preview** - View track lists within clusters before playlist creation
- 📅 **Genre Filter** - Exclude already-organized genres from new suggestions
- 📅 **Advanced Reports** - Genre trends, artist discovery, playlist insights
- 📅 **Web Interface** - Browse and analyze your library in a browser
- 📅 **Audio Features** - Exploring third-party APIs and local analysis tools
- 📅 **Playlist Sync Back** - Detect and sync manual changes to generated playlists

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
   - **Note:** Apps created after Nov 27, 2024 cannot access audio features API

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
╔══════════════════════════════════════╗
║   Spotify Tools - Main Menu         ║
╚══════════════════════════════════════╝
  1. Full Sync (Import all data)
  2. Incremental Sync (Update changes only)
  3. Partial Sync (Select stages)
  4. Genre Analysis
  5. Explore Genre Clusters & Playlists
  6. View Last Sync Status
  7. View Sync History
  8. Track Detail Report
  9. Exit
```

### Full Sync

Option 1 performs a complete import:
1. Authenticates with Spotify (opens browser)
2. Fetches all saved tracks with metadata
3. Fetches artist details with genres and follower counts
4. Fetches album details with labels and release dates
5. Fetches all user playlists **including tracks not in your saved library**
6. Syncs audio features (currently limited - see API restrictions below)

**Time Estimate:**
- ~30-45 minutes per 3,000 tracks (due to API rate limiting)
- Progress updates shown in real-time

### Incremental Sync

Option 2 performs a fast update of changed data only:
1. Checks last sync date (falls back to full sync if >30 days)
2. Fetches only **new tracks** added since last sync (filtered by date)
3. Enriches artist/album stubs created during previous syncs
4. Refreshes metadata for artists/albums not updated in 7+ days
5. Checks playlist SnapshotIds and re-syncs only changed playlists
6. Much faster than full sync - typically completes in 2-5 minutes

**When to Use:**
- Daily or weekly library updates
- After adding new tracks to Spotify
- After modifying playlists
- To keep metadata fresh without re-importing everything

**Benefits:**
- Significantly faster than full sync (processes only changes)
- Lower API usage (fewer rate limit concerns)
- Automatic stub enrichment (completes partial data from playlists)
- Smart playlist detection (only re-syncs modified playlists)

### View Sync Status

Options 2 & 3 show:
- Last sync completion time
- Statistics (tracks, artists, albums processed)
- Success/failure status
- Historical sync data

## Database Schema

### Naming Convention
All database tables and columns use **snake_case** naming (e.g., `track_id`, `duration_ms`, `first_synced_at`), following PostgreSQL conventions.

### Core Tables
- **tracks** - Track metadata (name, duration, popularity, ISRC, added dates)
- **artists** - Artist data (name, genres, popularity, followers)
- **albums** - Album information (name, release date, label, album type)
- **playlists** - User playlists
- **audio_features** ⚠️ - Exists but unpopulated (Spotify API restricted)
- **audio_analyses** ⚠️ - Exists but unpopulated (Spotify API restricted)

### Relationship Tables
- **track_artists** - Many-to-many with artist position tracking
- **track_albums** - Track-album relationships with disc/track numbers
- **playlist_tracks** - Playlist contents with positions

### Metadata Tables
- **sync_history** - Tracks all sync operations
- **spotify_tokens** - OAuth tokens (future use)

### Analytics Views

6 fully functional views for data visualization and analysis:

1. **v_tracks_with_artists** - Denormalized track-artist relationships
2. **v_tracks_with_albums** - Tracks with album details
3. **v_playlist_contents** - Playlist contents with track details
4. **v_genre_stats** - Genre statistics with track counts and popularity ⭐
5. **v_artist_performance** - Artist metrics and analytics ⭐
6. **v_sync_summary** - Human-readable sync history

**Limited functionality (audio features columns will be NULL):**
7. **v_track_complete_details** - Complete track info (audio features unavailable)
8. **v_high_energy_tracks** - Cannot filter by energy/danceability (no data)

See **DOCKER.md** for view descriptions and example queries.

## Important: Spotify API Restrictions (Jan 2026)

**Audio Features API Unavailable:** On November 27, 2024, Spotify restricted access to the `/v1/audio-features` endpoint for new applications. This means:

- ❌ Cannot fetch: tempo, key, mode, danceability, energy, valence, acousticness, etc.
- ✅ Can still fetch: tracks, artists, albums, playlists, genres, popularity
- 📊 Focus shifted to: genre analysis, artist insights, playlist trends, temporal patterns

**What You Can Still Analyze:**
- Genre distribution and popularity across your library
- Top artists by followers, track count, and popularity
- Album release trends over time
- Playlist composition and track overlap
- Library growth and listening history patterns
- Artist discovery and genre exploration

**Future Plans:**
- Exploring third-party APIs (Cyanite, Soundcharts)
- Local audio analysis tools (Essentia, similar solutions)
- Alternative data enrichment sources

## Configuration

### Database (PostgreSQL)
- **Port:** 5433 (mapped from container)
- **Database:** spotify_tools
- **User:** spotify_user
- **Password:** Set in `.env` file

### Spotify API
- **Rate Limit:** 30 requests/minute (configurable in code)
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
- CLI interface with Spectre.Console
- Full library import (tracks, artists, albums, playlists)
- Database views for analytics

### Phase 6: Complete ✅
- **Genre Clustering & Playlist Organization:**
  - ✅ Genre analysis with overlap detection
  - ✅ Auto-suggested genre clusters (10 predefined patterns + individual large genres)
  - ✅ Interactive cluster refinement (view all genres, remove genres that don't fit)
  - ✅ Smart orphaned genre handling (create new clusters, unclustered bucket, suggestions)
  - ✅ Cluster persistence with full CRUD operations
  - ✅ Track exclusion system for fine-tuning cluster playlists
  - ✅ Spotify playlist generation from finalized clusters

- **Analytics and Reporting:**
  - ✅ Genre analysis and distribution reports
  - ✅ Artist insights and discovery
  - ✅ Track detail reports with complete metadata
  - ✅ Cluster playlist reports with track counts

- **Sync Improvements:**
  - ✅ Incremental sync for fast updates (new tracks, stale metadata, changed playlists)
  - ✅ Fixed playlist sync bugs (position calculation, playlist-only tracks)
  - ✅ Complete metadata syncing for all tracks (including non-library playlist tracks)

### Phase 7: Current Focus ⏳
- **Enhanced Analytics:**
  - Interactive visualizations
  - Custom report generation
  - Export capabilities (CSV, JSON)

- **Audio Features Alternatives:**
  - Integration with third-party APIs
  - Local audio analysis pipeline
  - Alternative data enrichment

### Phase 8: Planned 📅
- **Track Preview in Clusters** - View full track lists before playlist creation
- **Genre Filter** - Exclude already-organized genres from new cluster suggestions
- **Playlist Composition Analysis** - Track overlap, diversity metrics
- **Temporal Trends** - Library growth charts, release date patterns
- **Advanced Reports** - Exportable analytics (CSV, JSON)

### Future Phases
- Web interface (ASP.NET Core with Blazor)
- Advanced recommendations engine
- External data integration (MusicBrainz, Last.fm)
- Backup and restore functionality
- Playlist sync back (detect manual changes to generated playlists)

## Contributing

This is a personal project, but suggestions and feedback are welcome!

## License

MIT License - Feel free to use and modify for your own purposes.

## Acknowledgments

- Built with [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)
- Powered by [Entity Framework Core](https://github.com/dotnet/efcore)
- Database: [PostgreSQL](https://www.postgresql.org/)
