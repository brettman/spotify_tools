# Web UI Architecture

## Overview

This document outlines the architecture for the Spotify Tools web interface, which provides playlist management functionality based on genres, clusters, and existing playlists.

## Technology Stack

### Backend
- **ASP.NET Core 8.0** - Web API + Blazor Server hosting
- **Existing Services** - Reuse `IAnalyticsService`, `ISyncService`, `IUnitOfWork`
- **PostgreSQL** - Existing database schema (no changes needed)

### Frontend
- **Blazor Server** - C# components with SignalR for reactivity
- **Blazor.Virtualizer** - Efficient rendering of large lists
- **HTML/CSS** - Bootstrap 5 for responsive layout
- **JavaScript** - Minimal (drag-drop library integration only)

## Architecture Decisions

### API Layer (Required)

Even though Blazor Server can directly inject services, we're implementing a full REST API layer for:

1. **Clean Architecture** - Proper separation of concerns
2. **Future-Proofing** - Easy migration to Blazor WASM or mobile apps
3. **Testability** - API endpoints can be tested independently
4. **Standard Patterns** - Familiar REST conventions for backend developers
5. **Swagger Documentation** - Auto-generated API docs

**Hosting Model:** API and Blazor Server run in the **same process** (minimal overhead)

```
┌─────────────────────────────────────────────────────┐
│            SpotifyTools.Web (Single Process)         │
├─────────────────────────────────────────────────────┤
│  Blazor Components    HTTP Client (in-memory)       │
│         ↓                    ↓                       │
│  API Controllers ← IAnalyticsService, IUnitOfWork    │
│         ↓                                            │
│  SpotifyTools.Data (EF Core) → PostgreSQL           │
└─────────────────────────────────────────────────────┘
```

## UI Design: Three-Panel Layout

### Layout Structure

```
┌──────────────────────────────────────────────────────────────────┐
│  Header: Search, Filters, "Create Playlist" Button              │
├────────────────┬──────────────────────────┬──────────────────────┤
│   LEFT PANEL   │     CENTER PANEL         │    RIGHT PANEL       │
│  (Filter)      │   (Browse & Select)      │   (Destination)      │
│                │                          │                      │
│ Genre Filters  │  Track List              │  Target Playlist     │
│ ┌────────────┐ │ ┌──────────────────────┐ │ ┌────────────────┐ │
│ │□ Rock (247)│ │ │☑ Track 1  Artist A ⋮ │ │ │🎵 My Rock Mix  │ │
│ │☑ Pop (189) │ │ │☑ Track 2  Artist B ⋮ │ │ │  ├─ Track A    │ │
│ │□ Metal(93) │ │ │□ Track 3  Artist C ⋮ │ │ │  ├─ Track B    │ │
│ │□ Jazz (156)│ │ │                      │ │ │  └─ Track C    │ │
│ └────────────┘ │ │[Virtualized Scroll]  │ │ │                │ │
│                │ └──────────────────────┘ │ │[+ New Playlist]│ │
│ Saved Clusters │                          │ │                │ │
│ ┌────────────┐ │ ☑ Select All             │ └────────────────┘ │
│ │Rock & Alt  │ │ [Add 2 to Playlist ▼]    │                    │
│ │Pop & Dance │ │                          │                    │
│ └────────────┘ │ Showing: Pop (189 tracks)│                    │
└────────────────┴──────────────────────────┴──────────────────────┘
```

### Panel Responsibilities

#### Left Panel: Filters
- **Genre list** with track counts (checkbox filters)
- **Saved clusters** (expandable accordion)
- **Quick filters:**
  - Not in any playlist
  - Added in last 30 days
  - Popularity threshold
  - Multiple genres

#### Center Panel: Track Browser
- **Virtualized track list** (renders only visible items)
- **Multi-select checkboxes** (with Shift+click ranges)
- **Bulk action toolbar** when items selected
- **Search/sort controls** at top
- **Genre tag pills** per track (click to filter)
- **Drag source** for individual tracks

#### Right Panel: Playlist Manager
- **Active playlist view** with current tracks
- **Create new playlist** button
- **Playlist selector dropdown**
- **Drop target** for drag-drop
- **Statistics:** Total tracks, duration, duplicates

## Data Loading Strategy

### Progressive Loading Pattern

**Problem:** Libraries can contain 5,000-10,000 tracks. Loading all at once = slow UI.

**Solution:** Multi-stage progressive loading

#### Stage 1: Metadata (Fast - ~100ms)
```http
GET /api/genres
Response: [
  { "name": "rock", "trackCount": 247 },
  { "name": "pop", "trackCount": 189 },
  ...
]
```

#### Stage 2: Paginated Tracks (On-demand)
```http
GET /api/genres/rock/tracks?page=1&pageSize=50
Response: {
  "items": [ /* 50 tracks */ ],
  "totalCount": 247,
  "page": 1,
  "pageSize": 50
}
```

#### Stage 3: Lazy Loading (Scroll-triggered)
- Blazor.Virtualizer automatically fetches next page as user scrolls
- Only renders ~20-30 visible items in DOM
- Total memory: ~2-3 pages in memory at once

### Caching Strategy

**Client-side (Blazor):**
- Genre list: Cache for session
- Track pages: Cache last 3 pages per genre
- Playlists: Cache and invalidate on mutation

**Server-side:**
- No caching in API layer (services are scoped, EF handles caching)
- Optional: Add `IMemoryCache` for genre counts (updated on sync)

## API Design

### Endpoints

#### Genres
```
GET    /api/genres                           # List all genres with counts
GET    /api/genres/{name}/tracks             # Paginated tracks for genre
```

#### Tracks
```
GET    /api/tracks                           # Paginated all tracks
GET    /api/tracks/search?q={query}          # Search tracks
GET    /api/tracks/{id}                      # Single track details
```

#### Playlists
```
GET    /api/playlists                        # List user playlists
GET    /api/playlists/{id}                   # Playlist details with tracks
POST   /api/playlists                        # Create new playlist
PUT    /api/playlists/{id}                   # Update playlist metadata
DELETE /api/playlists/{id}                   # Delete playlist
POST   /api/playlists/{id}/tracks            # Add tracks (bulk)
DELETE /api/playlists/{id}/tracks/{trackId}  # Remove track
POST   /api/playlists/{id}/sync              # Sync to Spotify
```

#### Clusters
```
GET    /api/clusters                         # List saved clusters
GET    /api/clusters/{id}                    # Cluster details
POST   /api/clusters                         # Create/save cluster
PUT    /api/clusters/{id}                    # Update cluster
DELETE /api/clusters/{id}                    # Delete cluster
POST   /api/clusters/{id}/finalize           # Mark as finalized
POST   /api/clusters/{id}/create-playlist    # Generate Spotify playlist
```

#### Analytics
```
GET    /api/analytics/genre-analysis         # Genre analysis report
GET    /api/analytics/sync-history           # Recent sync history
```

### DTOs (Data Transfer Objects)

```csharp
// Response DTOs
public class GenreDto
{
    public string Name { get; set; }
    public int TrackCount { get; set; }
    public int ArtistCount { get; set; }
}

public class TrackDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<ArtistSummaryDto> Artists { get; set; }
    public string AlbumName { get; set; }
    public int DurationMs { get; set; }
    public int Popularity { get; set; }
    public List<string> Genres { get; set; }
    public bool Explicit { get; set; }
}

public class ArtistSummaryDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<string> Genres { get; set; }
}

public class PlaylistDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int TrackCount { get; set; }
    public bool IsPublic { get; set; }
    public string? SpotifyId { get; set; }
}

public class PlaylistDetailDto : PlaylistDto
{
    public List<TrackDto> Tracks { get; set; }
    public int TotalDurationMs { get; set; }
}

public class ClusterDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<string> Genres { get; set; }
    public int TrackCount { get; set; }
    public bool IsFinalized { get; set; }
    public bool IsAutoGenerated { get; set; }
    public string? SpotifyPlaylistId { get; set; }
}

// Pagination wrapper
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// Request DTOs
public class CreatePlaylistRequest
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
}

public class AddTracksRequest
{
    public List<string> TrackIds { get; set; }
}
```

## Feature Implementation Details

### 1. Virtualized Scrolling

**Component:** `Blazor.Virtualizer` (built-in .NET 6+)

```razor
<Virtualize Items="@tracks" Context="track">
    <TrackRow Track="@track" OnSelected="HandleTrackSelected" />
</Virtualize>
```

**How it works:**
- Renders only visible items + small buffer
- Automatically handles scroll events
- Dynamically loads more data via `ItemsProvider` delegate

### 2. Multi-Select with Bulk Actions

**Pattern:** Checkbox selection with state management

```razor
<div class="bulk-actions" style="@(selectedTracks.Any() ? "" : "display:none")">
    <span>@selectedTracks.Count selected</span>
    <button @onclick="SelectAll">Select All</button>
    <button @onclick="ClearSelection">Clear</button>
    <select @onchange="HandleBulkAction">
        <option value="">Add to Playlist...</option>
        @foreach (var playlist in playlists)
        {
            <option value="@playlist.Id">@playlist.Name</option>
        }
    </select>
</div>

@code {
    private HashSet<string> selectedTracks = new();
    
    private void HandleTrackSelected(string trackId, bool isSelected)
    {
        if (isSelected) selectedTracks.Add(trackId);
        else selectedTracks.Remove(trackId);
    }
}
```

### 3. Drag & Drop

**Library:** Minimal custom JavaScript or `Blazor.DragDrop` package

**Implementation:**
- Single track drag from center panel
- Drop zone on playlist in right panel
- Visual feedback (drag ghost, drop zone highlight)
- Fallback to checkbox + bulk action on mobile

### 4. Real-time Updates

**Scenario:** User creates playlist in another tab/CLI

**Solution:** SignalR hub for playlist mutations

```csharp
// Server broadcasts changes
await Clients.All.SendAsync("PlaylistCreated", playlistDto);

// Blazor component listens
[Inject] HubConnection HubConnection { get; set; }

protected override async Task OnInitializedAsync()
{
    HubConnection.On<PlaylistDto>("PlaylistCreated", (playlist) =>
    {
        playlists.Add(playlist);
        StateHasChanged();
    });
}
```

## Performance Considerations

### Expected Load
- **Tracks:** 5,000-10,000 items
- **Genres:** 100-500 items
- **Playlists:** 10-100 items
- **Concurrent users:** 1-10 (personal/small team tool)

### Optimization Strategies

1. **Virtualization** - Only render visible items (20-30 in DOM)
2. **Pagination** - Server-side paging (50-100 items per page)
3. **Lazy Loading** - Load data on-demand, not upfront
4. **Debouncing** - Search input debounced (300ms)
5. **Caching** - Cache genre list, invalidate on sync
6. **Indexing** - Ensure DB indexes on `Genres`, `TrackId`, `PlaylistId`

### Performance Targets
- **Initial page load:** <2 seconds
- **Genre filter change:** <500ms
- **Search results:** <1 second
- **Add tracks to playlist:** <2 seconds (for 50 tracks)

## Security Considerations

### Authentication
- **OAuth via Spotify** - Reuse existing `SpotifyClientService`
- **ASP.NET Core Identity** - Optional future addition for multi-user
- **For now:** Single-user mode (no auth required)

### API Security (Future)
- JWT tokens for API access
- CORS configuration for external clients
- Rate limiting on API endpoints

## Development Phases

### Phase 1: API Layer (Week 1)
- [x] Create `SpotifyTools.Web` project
- [ ] Define DTOs for all entities
- [ ] Implement controllers:
  - [ ] GenresController
  - [ ] TracksController
  - [ ] PlaylistsController
  - [ ] ClustersController
- [ ] Add Swagger/OpenAPI
- [ ] Test with Postman/curl

### Phase 2: Blazor UI Foundation (Week 2)
- [ ] Create three-panel layout
- [ ] Implement ApiClientService (typed HTTP client)
- [ ] Build genre list component (left panel)
- [ ] Build track list component (center panel) - basic
- [ ] Build playlist panel (right panel) - basic
- [ ] Wire up navigation between panels

### Phase 3: Advanced Features (Week 3)
- [ ] Add Blazor.Virtualizer to track list
- [ ] Implement multi-select checkboxes
- [ ] Add bulk actions toolbar
- [ ] Implement search/filter
- [ ] Add genre tag pills
- [ ] Create playlist modal

### Phase 4: Polish (Week 4)
- [ ] Add drag-drop support
- [ ] Implement real-time updates (SignalR)
- [ ] Add loading states and error handling
- [ ] Implement duplicate detection
- [ ] Add playlist conflict warnings
- [ ] Responsive design (mobile/tablet)

### Phase 5: Integration (Week 5)
- [ ] Cluster management UI
- [ ] Create Spotify playlists from clusters
- [ ] Track exclusions UI
- [ ] Sync progress visualization
- [ ] Export/import playlists

## Testing Strategy

### API Testing
- **Unit tests:** Controller logic with mocked services
- **Integration tests:** Full request/response cycle with test DB
- **Tools:** xUnit, WebApplicationFactory, Testcontainers (PostgreSQL)

### UI Testing
- **Component tests:** Blazor component isolation tests (bUnit)
- **E2E tests:** Playwright for critical workflows
- **Manual testing:** Browser DevTools, Lighthouse performance audits

## Deployment Considerations

### Hosting Options
1. **Docker** - Existing `docker-compose.yml` + web service
2. **IIS/Nginx** - Traditional web server deployment
3. **Azure App Service** - Cloud hosting (future)

### Configuration
- `appsettings.json` for connection strings (existing)
- Environment variables for sensitive data
- Startup checks for database connectivity

## Open Questions / Future Enhancements

1. **Multi-user support?** - Currently single-user, add authentication later?
2. **Offline mode?** - PWA with service workers?
3. **Mobile app?** - REST API enables future mobile development
4. **Collaborative playlists?** - Real-time multi-user editing?
5. **AI recommendations?** - Suggest tracks for playlists based on similarity?

## References

- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
- [Blazor Virtualization](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization)
- [SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)
