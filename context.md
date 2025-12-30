# Spotify Tools - Project Context

**Last Updated:** 2025-12-30

## Project Status: Architecture Redesign Phase

### Current Phase
Planning and implementing major architecture refactoring to support local PostgreSQL database storage with analytics capabilities.

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

### Immediate (Current Session)
1. ✅ Create .gitignore
2. ✅ Create this context.md file
3. ✅ Commit refactoring work (commit `8afb4ee`)
4. ✅ Architecture implementation (commit `1076723`):
   - ✅ Create project structure (Domain, Data, Sync, Analytics)
   - ✅ Set up Docker PostgreSQL with docker-compose
   - ✅ Define domain models (10 entities, 2 enums)
   - ✅ Implement data layer (DbContext, repositories, Unit of Work)
   - ⏳ Build sync service
   - ⏳ Create CLI menu
   - ⏳ Implement analytics service

### Short Term
- Full import functionality
- Basic analytics reports
- Docker setup and documentation

### Medium Term
- Incremental sync
- Advanced analytics
- External data integration

### Long Term
- Web interface
- Multi-user support
- Automated scheduling

---

## Important Notes

### Configuration
- `appsettings.json` contains Spotify credentials (gitignored)
- Use `appsettings.json.template` as reference
- OAuth redirect URI must match port in code (currently 5009)

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
- [ ] Exact external data sources for enrichment
- [ ] CLI framework choice (simple menu vs System.CommandLine)
- [ ] Backup/export strategy for PostgreSQL data
- [ ] Token refresh interval and error handling strategy

---

## Reference Documentation
- See `CLAUDE.md` for project overview and instructions
- See `README.md` for user-facing documentation (to be created)
