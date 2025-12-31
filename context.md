# Spotify Tools - Project Context

**Last Updated:** 2025-12-31

## Project Status: Phase 6 Complete - Awaiting Quota Reset

### Current Phase
✅ **Phase 6 (Analytics) COMPLETE** - Audio analysis with section-by-section breakdown implemented
⚠️ **Spotify Daily API Quota Exhausted** - Can resume testing tomorrow after quota resets

### Session Summary (2025-12-31)
**Major Accomplishments:**
1. ✅ Implemented audio analysis feature with section-by-section key/tempo/time signature tracking
2. ✅ Fixed multiple rate limiting issues (60s cap, global backoff, retry wait logic)
3. ✅ Added Test Artist API debug tool
4. ⚠️ Discovered daily API quota limit (~10k-15k requests/day)

**Current Situation:**
- Daily API quota exhausted (21 hours until reset)
- All Spotify API calls blocked until tomorrow
- Database has partial sync (3,462 tracks, some artists/albums)
- Ready to complete full sync once quota resets

**Next Steps (Tomorrow):**
1. Test Artist API (option 5) to verify quota reset
2. Run ONE full sync - let it complete (~2-3 hours)
3. Test audio analysis feature with progressive rock track
4. Future syncs will be incremental (50-200 requests vs 4,200+)

---

## Project Vision

A tool to fetch Spotify library data (tracks, albums, artists, playlists), store it locally in PostgreSQL, and perform custom analytics focusing on audio features (tempo, key, etc.).

**Key Goals:**
- Offline access to Spotify library metadata
- Custom analytics and reporting (especially tempo/key analysis)
- Extensible for future external data sources (MusicBrainz, etc.)
- Multi-client support (CLI now, web interface later)
- Automated sync capabilities (cron jobs)

---

## Completed Work

### Phase 1: Initial Implementation ✅
**Commit:** `0080ea8` - Basic functioning client
- ✅ OAuth authentication with Spotify
- ✅ Fetch saved tracks with pagination
- ✅ Categorize tracks by artist genres
- ✅ Create genre-based playlists on Spotify

### Phase 2: Service Refactoring ✅
**Commit:** `8afb4ee`
- ✅ Created `ISpotifyClientService` interface
- ✅ Implemented `SpotifyClientWrapper` with proper OAuth handling
- ✅ Refactored `Program.cs` to use dependency injection
- ✅ Proper configuration management via `IConfiguration`
- ✅ Solution builds successfully

### Phase 3: Core Architecture ✅
**Commit:** `1076723`

**Docker Infrastructure:**
- ✅ PostgreSQL 16 container with docker-compose
- ✅ Health checks and persistent volumes
- ✅ Comprehensive DOCKER.md guide

**Domain Layer (SpotifyTools.Domain):**
- ✅ 10 entity classes with full relationships:
  - Track, Artist, Album, AudioFeatures (analytics focus)
  - Playlist, PlaylistTrack, TrackArtist, TrackAlbum
  - SpotifyToken, SyncHistory
- ✅ 2 enum types: SyncType, SyncStatus
- ✅ PostgreSQL-specific features (JSONB, arrays)

**Data Layer (SpotifyTools.Data):**
- ✅ SpotifyDbContext with entity configurations
- ✅ Repository pattern (generic + specialized)
- ✅ Unit of Work pattern with transaction support
- ✅ TrackRepository with analytics-focused queries
- ✅ Service registration extensions for DI
- ✅ EF Core 8.0 with Npgsql provider

### Phase 4: Sync Service Implementation ✅
**Completed:** 2025-12-31

**Sync Service (SpotifyTools.Sync):**
- ✅ Complete SyncService implementation (~600 lines)
- ✅ Full sync functionality for all data types
- ✅ Rate limiter (60 requests/min)
- ✅ Progress event system for real-time updates
- ✅ Sync history tracking with statistics
- ✅ Stub record strategy for foreign key integrity
- ✅ UTC DateTime handling for PostgreSQL compatibility
- ✅ Smart album/artist syncing (stub → full details)

**Features:**
- Tracks: Pagination (50/request), full metadata
- Artists: Individual fetch with genres, popularity, followers
- Albums: Full details with release dates, labels
- Audio Features: Batch processing (100/request) for tempo, key, etc.
- Playlists: User playlists with track positions
- Error handling and logging throughout

### Phase 5: CLI Interface ✅
**Completed:** 2025-12-31

**CLI Application (SpotifyGenreOrganizer → CLI):**
- ✅ Interactive menu system with professional UI
- ✅ Full dependency injection setup
- ✅ Database connection configuration (port 5433)
- ✅ Menu options:
  - Full Sync with real-time progress
  - View Last Sync Status with statistics
  - View Sync History (last 10 syncs)
  - Analytics placeholder
  - Exit
- ✅ Error handling and user feedback
- ✅ Duration tracking for sync operations

**Deployment Configuration:**
- ✅ PostgreSQL running on port 5433 (avoids local conflicts)
- ✅ Docker container healthy and accessible
- ✅ Database schema applied with migrations
- ✅ OAuth redirect URI: `http://127.0.0.1:5009/callback`
- ✅ EF Core version conflicts resolved (8.0.11)

**Testing Status:**
- ✅ OAuth authentication working
- ✅ Database connectivity verified
- ✅ Foreign key constraints resolved
- ✅ DateTime UTC issues fixed
- ✅ Full sync successfully running (3,462 tracks, 2,092 artists)

### Phase 6: Analytics Service ⏳
**Started:** 2025-12-31

**Analytics Service (SpotifyTools.Analytics):**
- ✅ IAnalyticsService interface with search and report methods
- ✅ AnalyticsService implementation (~200 lines)
- ✅ TrackDetailReport data model with nested classes
- ✅ ReportFormatter with beautiful CLI output
- ✅ CLI integration (menu option 4)

**Features Completed:**
- Track search by name (top 10 results)
- Comprehensive track detail report showing:
  - Basic track info (name, duration, popularity, ISRC, added date)
  - Artist details (name, genres, popularity, followers)
  - Album information (type, release date, label, total tracks)
  - Audio Features with visual bars:
    - Musical characteristics (tempo, key, mode, time signature, loudness)
    - Mood & feel (danceability, energy, valence)
    - Audio qualities (acousticness, instrumentalness, liveness, speechiness)
  - Playlists containing the track
- Key name translation (0 = C, 1 = C♯/D♭, etc.)
- Mode display (Major/Minor)
- Time signature display (4/4, 3/4, etc.)

**Audio Analysis Enhancement:** ✅
**Completed:** 2025-12-31 (Commit: 71c56fd)

- ✅ AudioAnalysis and AudioAnalysisSection domain entities
- ✅ EF Core migration for audio_analyses and audio_analysis_sections tables
- ✅ On-demand audio analysis fetch from Spotify API
- ✅ Automatic caching in PostgreSQL for offline access
- ✅ Section-by-section breakdown in Track Detail Report:
  - ✅ Overall track analysis (tempo, key, mode, time signature)
  - ✅ Timestamp for each section
  - ✅ Key changes highlighted with ► indicator
  - ✅ Tempo variations across sections
  - ✅ Time signature changes
  - ✅ Mode (Major/Minor) transitions
- ✅ Perfect for progressive rock (e.g., Jethro Tull's "Thick as a Brick") and jazz

---

## Planned Architecture (In Progress)

### Technology Stack
- **Language:** C# / .NET 8
- **Database:** PostgreSQL (Docker containerized)
- **ORM:** Entity Framework Core (initial), migrate to Dapper later
- **Auth:** OAuth with encrypted refresh token storage
- **Rate Limiting:** 60 requests/min to Spotify API

### Project Structure
```
SpotifyTools.sln
├── src/
│   ├── SpotifyTools.Domain/           # Shared entities/models
│   ├── SpotifyTools.Data/             # Data access layer (EF Core)
│   ├── SpotifyTools.Spotify/          # Spotify API client (rename from SpotifyClientService)
│   ├── SpotifyTools.Sync/             # Sync orchestration
│   ├── SpotifyTools.Analytics/        # Analytics & reporting (PRIMARY FOCUS)
│   └── SpotifyTools.CLI/              # Console interface
└── tests/                              # Future unit tests
```

### Database Schema
**Core Tables:**
- `tracks` - Track metadata (name, duration, popularity, ISRC, timestamps)
- `artists` - Artist info (name, genres, popularity, followers)
- `albums` - Album details (name, release date, label)
- `audio_features` - **KEY TABLE** (tempo, key, mode, energy, danceability, etc.)
- `playlists` - Playlist metadata
- `spotify_tokens` - Encrypted OAuth refresh tokens

**Relationships:**
- `track_artists` - Many-to-many with position tracking
- `track_albums` - Track/album linking with disc/track numbers
- `playlist_tracks` - Playlist contents with positions

**Sync Tracking:**
- `sync_history` - Track full/incremental sync operations

### Key Design Decisions

**1. Authentication Strategy**
- Store encrypted refresh tokens in PostgreSQL
- Support automated cron jobs
- Multi-account capable
- Smooth path to future web app

**2. Rate Limiting**
- Max 60 requests/min to stay under Spotify limits
- Prioritize completeness over speed
- Expected: ~6-7 hours for full import of 10k tracks

**3. Sync Strategy**
- **Phase 1:** Full import (all saved tracks, artists, albums, audio features, playlists)
- **Phase 2:** Incremental sync (compare snapshots, fetch new/changed only)
- Manual trigger via CLI, cron-job capable

**4. Analytics Focus**
- Tempo analysis and distribution
- Key/mode distribution (for DJ mixing)
- Genre statistics
- Custom queries via dedicated Analytics service layer
- Extensible report interface

**5. Future Extensibility**
- PostgreSQL JSONB for flexible extended metadata
- Support for external data sources (MusicBrainz for producers/mixers)
- Web interface migration path
- Dapper migration for complex analytics queries

---

## Next Steps

### Immediate (Current Session) ✅
1. ✅ Create .gitignore
2. ✅ Create this context.md file
3. ✅ Commit refactoring work (commit `8afb4ee`)
4. ✅ Architecture implementation (commit `1076723`):
   - ✅ Create project structure (Domain, Data, Sync, Analytics)
   - ✅ Set up Docker PostgreSQL with docker-compose
   - ✅ Define domain models (10 entities, 2 enums)
   - ✅ Implement data layer (DbContext, repositories, Unit of Work)
   - ✅ Build sync service
   - ✅ Create CLI menu
   - ⏳ Implement analytics service

### Current Focus (Phase 6 - COMPLETE ✅)
- ✅ Basic track detail report (COMPLETE)
- ✅ **Audio Analysis Enhancement (COMPLETE - Commit 71c56fd)**
  - ✅ Spotify Audio Analysis integration
  - ✅ Section-by-section key/tempo/time signature tracking
  - ✅ Structural analysis display with change indicators
  - ✅ On-demand fetch with PostgreSQL caching
- ✅ **Rate Limiting Improvements (Commits 337ea6e, d9f4203, 569e67a)**
  - ✅ 60-second max wait cap
  - ✅ Global backoff mechanism
  - ✅ Retry logic properly waits for backoff
  - ✅ Retry-After header logging
- ✅ **Debug Tooling (Commit 373fb54)**
  - ✅ Test Artist API menu option
  - ✅ Daily quota limit detection

### Next Session (After Quota Reset)
1. ⏳ Test Artist API to verify quota reset
2. ⏳ Complete initial full sync (one time, ~2-3 hours)
3. ⏳ Test audio analysis with progressive rock track
4. ⏳ Verify incremental sync performance

### Future Features
- ⏳ Tempo distribution analysis
- ⏳ Key/mode distribution for DJ mixing
- ⏳ Genre statistics from artist data
- ⏳ Advanced analytics reports
- ⏳ Incremental sync implementation (detect changes only)

### Short Term
- ✅ Full import functionality (DONE)
- ✅ Basic analytics reports (DONE)
- ✅ Docker setup and documentation (DONE)
- ⏳ Documentation updates (README, CLAUDE.md)
- ⏳ Performance optimization for large libraries

### Medium Term
- Incremental sync implementation
- Advanced analytics (correlation analysis, recommendations)
- External data integration (MusicBrainz)
- Performance optimization (caching, indexing)

### Long Term
- Web interface (ASP.NET Core or Blazor)
- Multi-user support with authentication
- Automated scheduling (background services)
- Export/backup functionality

---

## Important Notes

### Configuration
- `appsettings.json` contains Spotify credentials and database connection (gitignored)
- Use `appsettings.json.template` as reference
- **OAuth redirect URI:** `http://127.0.0.1:5009/callback` (must match in Spotify Dashboard)
- **Database:** PostgreSQL on port 5433 (Docker container)
- **Connection String:** `Host=localhost;Port=5433;Database=spotify_tools;Username=spotify_user;Password=...`

### Spotify API Notes
- Genres come from artists, not tracks
- Rate limits: ~180 req/min (we use 60 to be safe)
- Required scopes: `UserLibraryRead`, `PlaylistModifyPublic`, `PlaylistModifyPrivate`
- Audio features require separate API calls per track

### Architecture Principles
- **Single Responsibility Principle** - Each service has one clear purpose
- **Repository Pattern** - Abstract data access for testability
- **Dependency Injection** - Constructor injection throughout
- **Interface-based design** - Easy to mock and test

---

## Questions to Resolve
- [x] CLI framework choice → **Simple menu system (implemented)**
- [ ] Exact external data sources for enrichment (future: MusicBrainz)
- [ ] Backup/export strategy for PostgreSQL data
- [ ] OAuth token refresh strategy (currently browser-based, future: refresh token storage)
- [ ] Incremental sync implementation strategy
- [ ] Analytics visualization (CLI tables vs future web interface)

---

## Reference Documentation
- See `CLAUDE.md` for project overview and instructions
- See `README.md` for user-facing documentation (to be created)
