# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Spotify Tools is a C# application that syncs your Spotify library to PostgreSQL for offline access and custom analytics. The application uses OAuth authentication, fetches all saved tracks/artists/albums/playlists, and stores them in a PostgreSQL database for analysis. Features an interactive CLI built with Spectre.Console for browsing and visualizing your music library.

**Available Data for Analysis:**
- **Tracks**: Names, duration, popularity, explicit flag, ISRC codes, added dates
- **Artists**: Names, genres, popularity, follower counts
- **Albums**: Names, release dates, labels, track counts, album types
- **Playlists**: User playlists with track relationships and positions
- **Relationships**: Track-artist mappings, track-album mappings, playlist contents

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

## Spotify API Limitations (as of Jan 2026)

**Audio Features API Restricted:** On November 27, 2024, Spotify restricted access to the `/v1/audio-features` endpoint for new applications. This endpoint provided metrics like danceability, energy, tempo, valence, acousticness, etc.

**Impact:**
- Audio features sync is **disabled** in the application (marked as unavailable in the UI)
- The `audio_features` database tables exist but will remain unpopulated
- Analytics views that reference audio features will not have that data
- Apps created before Nov 27, 2024 may still have access (check your Spotify Developer Dashboard)

**Workarounds Being Explored:**
- Third-party APIs (Cyanite, Soundcharts, Musiio)
- Local audio analysis tools (Essentia, librosa equivalents)
- These will be implemented in future updates if needed

## Database Views for Analytics

8 pre-built PostgreSQL views for analytics and visualization:

**Currently Functional (with available data):**
1. **v_tracks_with_artists** - Denormalized track-artist relationships
2. **v_tracks_with_albums** - Tracks with album information
3. **v_playlist_contents** - Playlist contents with track and artist details
4. **v_artist_performance** - Artist metrics including track counts and averages
5. **v_sync_summary** - Human-readable sync history with duration calculations
6. **v_genre_stats** - Genre statistics with track counts and popularity

**Limited Functionality (audio features unavailable):**
7. **v_track_complete_details** - Complete track info (audio features columns will be NULL)
8. **v_high_energy_tracks** - Cannot filter by energy/danceability (no data)

Views use snake_case column naming and can be queried from DataGrip or psql.

**Analysis Possibilities with Current Data:**
- Genre analysis (most popular genres, genre distribution)
- Artist analysis (top artists by follower count, track count, popularity)
- Temporal analysis (library growth over time, release date trends)
- Playlist analysis (playlist sizes, track overlap between playlists)
- Popularity trends (most popular tracks/artists/albums)
- Album analysis (album types, label distribution, release years)

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
- `tracks`, `artists`, `albums`, `playlists` - Core music entities
- `track_artists`, `track_albums`, `playlist_tracks` - Relationship tables
- `audio_features`, `audio_analyses`, `audio_analysis_sections` - ⚠️ Not populated (Spotify API restricted)
- `sync_history` - Sync execution tracking
- `spotify_tokens` - OAuth token storage

**Views:** 8 pre-built analytics views for data visualization (see Database Views section for details)

### Key Dependencies

- **SpotifyAPI.Web** (7.2.1): Main Spotify API wrapper
- **Entity Framework Core** (8.0.11): ORM with PostgreSQL provider (Npgsql)
- **EFCore.NamingConventions** (8.0.3): Enforces snake_case naming
- **Spectre.Console** (0.49.1): Beautiful CLI tables, menus, progress bars
- **PostgreSQL 16**: Database (Docker container)

## Genre Clustering & Playlist Organization (New Feature)

**Status:** In active development (Jan 2026)

**Goal:** Organize user's Spotify library into genre-based playlists using intelligent clustering.

### Current Implementation

**Analytics Service Methods:**
- `GetGenreAnalysisReportAsync()` - Comprehensive genre landscape analysis
- `GetAvailableGenreSeedsAsync()` - Fetches official Spotify genre seeds from `/recommendations/available-genre-seeds`
- `SuggestGenreClustersAsync(minTracks)` - Auto-generates genre clusters using pattern matching
- `GetTracksByGenreAsync()` - Maps tracks to genres via artist relationships
- `GetClusterPlaylistReportAsync(cluster)` - Generates detailed track list for a cluster

**Clustering Algorithm:**
- Uses 10 predefined patterns (Rock & Alternative, Pop & Dance, Electronic & EDM, Hip Hop & Rap, R&B & Soul, Metal & Heavy, Jazz & Blues, Folk & Acoustic, Classical & Orchestral, Latin & World)
- Matches library genres to patterns using keyword matching
- Creates individual clusters for large remaining genres (40+ tracks)
- Filters to minimum 20 tracks per cluster

**Interactive Cluster Refinement (UI):**
1. View suggested clusters with track/artist counts
2. Select cluster to review
3. See ALL genres in cluster with detailed breakdown
4. Multi-select removal of genres that don't fit (e.g., remove "smooth jazz" from "hard bop")
5. **Orphaned Genre Handling (Option D):**
   - Shows removed genres with track counts
   - Options: Create new clusters (if 20+ tracks), add to "Unclustered" bucket, suggest alternatives, or leave unclustered
   - Large genres (20+ tracks) can become standalone clusters
   - Small genres go to "Unclustered" for later review

**Models:**
- `GenreCluster` - Represents a cluster with name, genres list, track/artist counts
- `GenreAnalysisReport` - Full genre landscape with overlaps and statistics
- `ClusterPlaylistReport` - Track details for a cluster (artist, song, duration, album, genres)

**UI Components:**
- Main menu: "Explore Genre Clusters & Playlists"
- Cluster summary table with track counts and percentages
- Interactive cluster review with genre breakdown
- Multi-select genre removal interface
- Orphaned genre handler with smart suggestions

### Known Limitations & Next Steps

**Current Limitations:**
- Cluster refinements are preview-only (not persisted)
- No track preview within clusters yet
- Alternative cluster suggestions not implemented
- Cannot generate actual Spotify playlists yet

**Planned Features:**
1. Save/persist refined clusters to database
2. Track list preview (Artist | Song | Duration | Album | Genre)
3. Alternative cluster suggestions using genre overlap analysis
4. Spotify playlist generation from approved clusters
5. Unclustered genre tracking and management
6. Custom cluster creation from scratch

**User Feedback Incorporated:**
- Genre clustering must respect subgenre differences (e.g., "hard bop" ≠ "smooth jazz")
- Interactive refinement needed instead of automatic clustering
- Removed genres need intelligent handling (not just dropped)

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
