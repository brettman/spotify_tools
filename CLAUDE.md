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

## Genre Clustering & Playlist Organization

**Status:** ✅ Core functionality complete (Jan 2026)

**Goal:** Organize user's Spotify library into genre-based playlists using intelligent clustering with full persistence and management capabilities.

### Complete Implementation

**Analytics Service Methods:**
- `GetGenreAnalysisReportAsync()` - Comprehensive genre landscape analysis
- `GetAvailableGenreSeedsAsync()` - Fetches official Spotify genre seeds
- `SuggestGenreClustersAsync(minTracks)` - Auto-generates genre clusters using pattern matching
- `GetTracksByGenreAsync()` - Maps tracks to genres via artist relationships
- `GetClusterPlaylistReportAsync(cluster)` - Generates detailed track list for a cluster
- ✅ `SaveClusterAsync(cluster, customName)` - Persists refined clusters to database
- ✅ `GetSavedClustersAsync()` - Loads all saved clusters
- ✅ `GetSavedClusterByIdAsync(id)` - Loads specific cluster
- ✅ `UpdateClusterAsync(id, cluster)` - Updates existing cluster
- ✅ `DeleteClusterAsync(id)` - Deletes cluster
- ✅ `FinalizeClusterAsync(id)` - Marks cluster ready for playlist generation

**Database Persistence:**
- **Table:** `saved_clusters` (snake_case columns)
- **Entity:** `SavedCluster` with fields: id, name, description, genres (CSV), primary_genre, is_auto_generated, created_at, updated_at, is_finalized
- **Repository:** `SavedClusterRepository` with specialized queries
- **Migration:** `20260103095757_AddSavedClustersTableSnakeCase`

**Clustering Algorithm:**
- Uses 10 predefined patterns (Rock & Alternative, Pop & Dance, Electronic & EDM, Hip Hop & Rap, R&B & Soul, Metal & Heavy, Jazz & Blues, Folk & Acoustic, Classical & Orchestral, Latin & World)
- Matches library genres to patterns using keyword matching
- Creates individual clusters for large remaining genres (40+ tracks)
- Filters to minimum 20 tracks per cluster

**Complete Workflow:**
1. **Generate** suggested clusters from library
2. **Review & Refine** by removing genres that don't fit
3. **Save** refined clusters with custom names
4. **View** all saved clusters in management UI
5. **Edit** saved clusters to further refine
6. **Finalize** when ready for playlist generation
7. **Delete** unwanted clusters

**Interactive Cluster Refinement:**
- View suggested clusters with track/artist counts
- Select cluster to review ALL genres with detailed breakdown
- Multi-select removal of genres that don't fit (e.g., remove "smooth jazz" from "hard bop")
- Description automatically updates to reflect refined genre list
- **Orphaned Genre Handling (Option D):**
  - Shows removed genres with track counts
  - Options: Create new clusters (if 20+ tracks), add to "Unclustered" bucket, suggest alternatives, or leave unclustered
  - Large genres (20+ tracks) can become standalone clusters
  - Small genres go to "Unclustered" for later review

**Cluster Management UI:**
- View all saved clusters in a table (ID, name, tracks, genres, status, type)
- Select cluster to view details (description, genre list, track counts)
- Edit cluster genres (remove unwanted genres, updates description)
- Delete clusters with confirmation
- Finalize clusters for playlist generation
- All operations persist to database

**Models:**
- `GenreCluster` - In-memory cluster representation (id, name, description, genres list, track/artist counts, percentage)
- `SavedCluster` - Database entity (persisted clusters)
- `GenreAnalysisReport` - Full genre landscape with overlaps and statistics
- `ClusterPlaylistReport` - Track details for a cluster

**Known Issues:**
See `issues.md` for tracked bugs and UX improvements:
- Minor: Cannot exit edit screen without making changes
- Medium: Already-organized genres still appear in new suggestions

**Implemented Features:**
1. ✅ Spotify playlist generation from finalized clusters (`CreatePlaylistFromClusterAsync`)
2. ✅ Track exclusion system (`ExcludeTrackAsync`, `IncludeTrackAsync`, `GetExcludedTrackIdsAsync`)
3. ✅ Playlist persistence with database fields (`spotify_playlist_id`, `playlist_created_at`)

**Planned Features:**
1. Track list preview within clusters (Artist | Song | Duration | Album | Genre)
2. Filter already-organized genres from new suggestions
3. Unclustered genre tracking and management
4. Alternative cluster suggestions using genre overlap analysis
5. Custom cluster creation from scratch
6. Playlist sync back (detect manual changes to generated playlists)

**User Feedback Incorporated:**
- Genre clustering must respect subgenre differences (e.g., "hard bop" ≠ "smooth jazz")
- Interactive refinement needed instead of automatic clustering
- Removed genres need intelligent handling (not just dropped)
- Description must reflect actual refined genre list, not original

## Sync System

### Sync Types

**Full Sync (`FullSyncAsync`):**
- Imports all saved tracks, artists, albums, and playlists
- Creates artist/album stubs for quick initial import
- **Critical Fix (Jan 2026):** Now syncs ALL playlist tracks, including those not in saved library
- Creates complete metadata for playlist-only tracks (artists, albums, relationships)
- Time: ~30-45 minutes per 3,000 tracks (rate limited)

**Incremental Sync (`IncrementalSyncAsync`):**
- **Status:** ✅ Fully implemented (Jan 2026)
- Smart update that only processes changes since last sync
- Automatic fallback to full sync if no previous sync or >30 days old

**How Incremental Sync Works:**
1. **New Tracks** - Filters by `AddedAt` date, syncs only tracks added since last sync
2. **Artist Stubs** - Enriches stubs (Genres.Length == 0) with full metadata
3. **Stale Artists** - Refreshes artists with `LastSyncedAt` > 7 days old
4. **Album Stubs** - Enriches stubs (Label is null/empty) with full metadata
5. **Stale Albums** - Refreshes albums with `LastSyncedAt` > 7 days old
6. **Changed Playlists** - Uses `SnapshotId` comparison to detect changes, only re-syncs modified playlists

**Configuration Constants:**
- `METADATA_REFRESH_DAYS = 7` - How often to refresh artist/album metadata
- `FULL_SYNC_FALLBACK_DAYS = 30` - Max days between syncs before forcing full sync

**Benefits:**
- Much faster than full sync (typically 2-5 minutes vs 30-45 minutes)
- Lower API usage (fewer rate limit concerns)
- Automatic stub enrichment (completes partial data from playlists)
- Smart change detection (only processes what's new/changed)

### Critical Sync Bug Fixes (Jan 2026)

**Bug 1: Playlist Track Position Calculation**
- **Issue:** Used `offset + IndexOf(item)` AFTER offset was incremented
- **Fix:** Introduced `globalPosition` counter that increments sequentially
- **Impact:** Track positions now accurately reflect playlist order

**Bug 2: Missing Playlist Tracks (CRITICAL)**
- **Issue:** Tracks in playlists but not in saved library were silently skipped
- **Fix:** Changed from `continue` to full track sync with complete metadata
- **Impact:** All playlist tracks now sync, regardless of saved library status
- **Example:** "80s Phoenix Radio" now syncs all 98 tracks instead of just 12

**Implementation Details:**
- Creates artist stubs for playlist-only track artists
- Creates album stubs for playlist-only track albums
- Establishes all relationship records (TrackArtist, TrackAlbum)
- Ensures referential integrity for all tracks

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
