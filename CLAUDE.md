# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Spotify Tools is a C# application that syncs your Spotify library to PostgreSQL for offline access and custom analytics. The application uses OAuth authentication, fetches all saved tracks/artists/albums/playlists, and stores them in a PostgreSQL database with audio features for analysis. Features an interactive CLI built with Spectre.Console for browsing and visualizing your music library.

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
- **Connection String**: PostgreSQL connection string with host, port (5433), database name, username, and password
- **Redirect URI**: Must be `http://127.0.0.1:5009/callback` (matches Spotify Developer Dashboard)

## Database Views

8 pre-built PostgreSQL views for analytics and visualization:

1. **v_tracks_with_artists** - Denormalized track-artist relationships
2. **v_tracks_with_albums** - Tracks with album information
3. **v_track_complete_details** - Complete track info with aggregated artists, genres, albums, audio features
4. **v_playlist_contents** - Playlist contents with track and artist details
5. **v_genre_stats** - Genre statistics with track counts, popularity, audio feature averages
6. **v_artist_performance** - Artist metrics including track counts and averages
7. **v_sync_summary** - Human-readable sync history with duration calculations
8. **v_high_energy_tracks** - Pre-filtered high-energy tracks (energy > 0.7, danceability > 0.6)

Views use snake_case column naming and can be queried from DataGrip or psql.

## Architecture

### Clean Architecture Multi-Project Solution

```
SpotifyTools/
├── SpotifyTools.Domain        # Entity models (Track, Artist, Album, AudioFeatures, etc.)
├── SpotifyTools.Data          # EF Core, repositories, Unit of Work pattern
├── SpotifyTools.Sync          # Sync orchestration with rate limiting and progress events
├── SpotifyTools.Analytics     # Analytics queries and track detail reports
├── SpotifyClientService       # Spotify API wrapper with OAuth
└── SpotifyGenreOrganizer      # CLI interface with Spectre.Console
    ├── CliMenuService.cs      # Main menu orchestration
    └── UI/
        ├── MenuBuilder.cs         # Spectre.Console menu factory
        ├── NavigationService.cs   # Track browsing (artist/playlist/genre/search)
        ├── ProgressAdapter.cs     # Sync progress visualization
        └── SpectreReportFormatter.cs  # Rich table/panel rendering
```

### Database Schema (PostgreSQL)

**Naming Convention:** All tables and columns use **snake_case** (e.g., `track_id`, `duration_ms`, `first_synced_at`), enforced by `EFCore.NamingConventions` package.

**Tables:**
- `tracks`, `artists`, `albums`, `playlists`
- `track_artists`, `track_albums`, `playlist_tracks` (relationship tables)
- `audio_features`, `audio_analyses`, `audio_analysis_sections`
- `sync_history`, `spotify_tokens`

**Views:** 8 pre-built analytics views for data visualization (see Database Views section below)

### Key Dependencies

- **SpotifyAPI.Web** (7.2.1): Main Spotify API wrapper
- **Entity Framework Core** (8.0.11): ORM with PostgreSQL provider (Npgsql)
- **EFCore.NamingConventions** (8.0.3): Enforces snake_case naming
- **Spectre.Console** (0.49.1): Beautiful CLI tables, menus, progress bars
- **PostgreSQL 16**: Database (Docker container)

## Important Implementation Details

### Database Migrations

Migrations are stored in `src/SpotifyTools.Data/Migrations/`. To create or apply migrations:

```bash
cd src/SpotifyTools.Data
dotnet ef migrations add MigrationName --startup-project ../SpotifyGenreOrganizer
dotnet ef database update --startup-project ../SpotifyGenreOrganizer
```

### Snake Case Naming Convention

The database uses snake_case for all tables and columns, configured via:
- `EFCore.NamingConventions` package (v8.0.3) in `SpotifyTools.Data`
- `.UseSnakeCaseNamingConvention()` in `Program.cs` DbContext configuration

Migration `20260102082536_ConvertToSnakeCase` converted all column names from PascalCase to snake_case.

### Rate Limiting

SyncService implements rate limiting (60 requests/minute) with progress events. Rate limiter delays requests to respect Spotify API limits.

### Spotify API Scopes

Required OAuth scopes (configured in `SpotifyClientService`):
- `UserLibraryRead`: Access saved tracks
- `PlaylistModifyPublic`: Create/modify public playlists
- `PlaylistModifyPrivate`: Create/modify private playlists

### Configuration Security

`appsettings.json` contains sensitive credentials and should be in `.gitignore`. Template file is `appsettings.json.template`.
