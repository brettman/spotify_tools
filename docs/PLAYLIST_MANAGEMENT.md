# Playlist Management Features

**Status:** ✅ Production Ready (January 10, 2026)

## Overview

Comprehensive playlist management system with manual Spotify sync, dirty tracking, and advanced search/filter capabilities. Designed to provide better playlist management than the native Spotify UI, especially for users with hundreds of playlists and thousands of songs.

---

## Features

### 1. Playlist Management Page (`/playlists`)

**Navigation:**
- Added to sidebar navigation menu
- Direct link from start page
- Accessible via `/playlists` route

**Core Functionality:**
- List all playlists with key metadata
- Real-time search and filtering
- Manual sync to Spotify with visual feedback
- Playlist detail modal with full track list
- Delete playlists with confirmation

---

### 2. Manual Spotify Sync

**Philosophy:** Local-first workflow with explicit sync control.

**Workflow:**
1. Work locally: Add/remove tracks from playlists in `/library`
2. Changes saved to local database immediately
3. Navigate to `/playlists` when ready to sync
4. Click "Sync to Spotify" or "Push Updates" button
5. Changes pushed to Spotify API

**Sync Operations:**
- **New Playlists (GUID):** Creates playlist on Spotify, uploads tracks
- **Existing Playlists (Spotify ID):** Updates existing playlist using ReplaceItems API
- **Local overwrites Spotify:** Changes made in Spotify directly are overwritten

**API Endpoint:**
```http
POST /api/playlists/{id}/sync
```

**Implementation:**
- `PlaylistService.SyncPlaylistToSpotifyAsync()`
- Handles both create and update scenarios
- Uses Spotify's `ReplaceItems` endpoint for updates
- Updates `LastSyncedAt` timestamp after successful sync

---

### 3. Dirty Tracking

**Database Schema:**
```sql
-- playlists table
last_modified_at  timestamp with time zone NOT NULL
last_synced_at    timestamp with time zone NOT NULL
```

**Tracking Logic:**
- `LastModifiedAt` updated when:
  - Creating a new playlist
  - Adding tracks to playlist
  - Removing tracks from playlist
- `LastSyncedAt` updated when:
  - Syncing playlist to Spotify
- Dirty when: `LastModifiedAt > LastSyncedAt`

**Visual Indicators:**
- 🟨 **Yellow "Has Changes"** - Local changes need syncing
- 🟩 **Green "Synced"** - Up-to-date with Spotify
- ⚪ **Gray "Local"** - Not yet on Spotify

**Migration:**
- File: `20260110212304_AddLastModifiedAtToPlaylists.cs`
- **Verified:** Uses snake_case (`last_modified_at`)

---

### 4. Search & Filter

**Search:**
- Real-time search by playlist name or description
- Case-insensitive matching
- Clear button (X) to reset search

**Filter Options:**
1. **All Playlists** - Show everything
2. **Has Changes** - Playlists with unsaved local changes ⭐ NEW
3. **Local Only** - Playlists not yet on Spotify (GUID)
4. **On Spotify** - Playlists synced to Spotify (Spotify ID)
5. **Empty Playlists** - Playlists with no tracks

**Sort Options:**
- By Name (alphabetical)
- By Track Count (most tracks first)
- By Type (local first, then Spotify)

**Performance:**
- Filter/sort operates on in-memory list
- Counts update dynamically
- No database queries on filter change

---

### 5. Playlist Detail Modal

**Triggered By:** Clicking eye icon on any playlist

**Shows:**
- Playlist metadata (description, track count, total duration)
- Public/Private status
- Sync status badge
- Full track list in scrollable table
- Track details:
  - Position number
  - Title with explicit content indicator
  - Artist(s)
  - Album name
  - Duration

**Implementation:**
- Modal overlay with Bootstrap styling
- Lazy loading: fetches playlist details on demand
- `PlaylistService.GetPlaylistByIdAsync()`
- Efficient single-query with projections

---

## API Endpoints

### GET `/api/playlists`
List all playlists with metadata.

**Response:**
```json
[
  {
    "id": "string",
    "name": "string",
    "description": "string",
    "trackCount": 0,
    "isPublic": true,
    "spotifyId": null,
    "lastSyncedAt": "2026-01-10T12:00:00Z",
    "lastModifiedAt": "2026-01-10T12:30:00Z"
  }
]
```

### GET `/api/playlists/{id}`
Get playlist details with full track list.

### POST `/api/playlists`
Create a new local playlist.

### POST `/api/playlists/{id}/tracks`
Add tracks to playlist (updates `LastModifiedAt`).

### DELETE `/api/playlists/{id}/tracks/{trackId}`
Remove track from playlist (updates `LastModifiedAt`).

### POST `/api/playlists/{id}/sync`
Sync playlist to Spotify (updates `LastSyncedAt`).

**Returns:**
```json
{
  "spotifyId": "string",
  "message": "Playlist synced to Spotify successfully"
}
```

### DELETE `/api/playlists/{id}`
Delete playlist.

---

## Database Schema

### playlists Table
```sql
id                   text PRIMARY KEY
name                 text NOT NULL
description          text
owner_id             text NOT NULL
is_public            boolean NOT NULL
snapshot_id          text NOT NULL
first_synced_at      timestamp with time zone NOT NULL
last_synced_at       timestamp with time zone NOT NULL
last_modified_at     timestamp with time zone NOT NULL  -- NEW
```

**Key Points:**
- `id` can be GUID (local) or Spotify ID (synced)
- `last_modified_at` tracks local changes
- `last_synced_at` tracks sync operations
- All timestamps use UTC

---

## Architecture

### Service Layer
**File:** `src/SpotifyTools.Web/Services/PlaylistService.cs`

**Key Methods:**
- `GetAllPlaylistsAsync()` - List playlists
- `GetPlaylistByIdAsync()` - Get details
- `CreatePlaylistAsync()` - Create local playlist
- `CreateAndSyncPlaylistAsync()` - Create and sync in one operation
- `AddTracksToPlaylistAsync()` - Add tracks (updates LastModifiedAt)
- `RemoveTrackFromPlaylistAsync()` - Remove tracks (updates LastModifiedAt)
- `SyncPlaylistToSpotifyAsync()` - Push to Spotify (updates LastSyncedAt)
- `DeletePlaylistAsync()` - Delete playlist

### UI Components
**File:** `src/SpotifyTools.Web/Pages/Playlists.razor`

**Features:**
- Search/filter/sort controls
- Playlist table with status indicators
- Action buttons (sync, view, delete)
- Playlist detail modal
- Summary statistics cards

### DTOs
**File:** `src/SpotifyTools.Web/DTOs/PlaylistDto.cs`

```csharp
public class PlaylistDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int TrackCount { get; set; }
    public bool IsPublic { get; set; }
    public string? SpotifyId { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class PlaylistDetailDto : PlaylistDto
{
    public List<TrackDto> Tracks { get; set; }
    public int TotalDurationMs { get; set; }
}
```

---

## Bug Fixes

### 1. Playlist Sync Race Condition (Fixed)
**Issue:** Tracks were added AFTER sync was called, causing "no tracks to sync" error.

**Fix:** Changed `CreateAndSyncPlaylistAsync()` to accept `trackIds` parameter and add tracks BEFORE syncing.

### 2. HttpClient BaseAddress Error (Fixed)
**Issue:** Blazor Server doesn't set HttpClient.BaseAddress by default.

**Fix:** Use `PlaylistService` directly instead of making HTTP calls to our own API.

### 3. Playlist Update Sync Not Working (Fixed)
**Issue:** Sync method would return early for playlists with Spotify IDs.

**Fix:** Added update path using Spotify's `ReplaceItems` API to update existing playlists.

---

## Usage Examples

### Create Local Playlist
```csharp
var request = new CreatePlaylistRequest
{
    Name = "My Playlist",
    Description = "Created locally",
    IsPublic = false
};
var playlist = await playlistService.CreatePlaylistAsync(request);
```

### Add Tracks and Sync
```csharp
// Add tracks (marks as modified)
await playlistService.AddTracksToPlaylistAsync(playlistId, trackIds);

// Sync to Spotify (creates or updates)
var spotifyId = await playlistService.SyncPlaylistToSpotifyAsync(playlistId);
```

### Check If Playlist Has Changes
```csharp
var hasChanges = playlist.LastModifiedAt > playlist.LastSyncedAt;
```

---

## Future Enhancements

### Planned Features
1. **Confirmation dialogs** for delete operations
2. **Bulk sync** - sync multiple playlists at once
3. **Auto-sync flag** per playlist (optional daemon integration)
4. **Conflict detection** - warn if Spotify SnapshotId changed
5. **Bidirectional sync** - detect and merge Spotify changes
6. **Playlist detail page** - dedicated route for editing

### Potential Improvements
1. **Track reordering** within playlists
2. **Playlist duplication** with one click
3. **Merge playlists** functionality
4. **Export to CSV/JSON**
5. **Playlist sharing** via public links
6. **Collaborative editing** indicators

---

## Testing

### Manual Test Plan

**Test 1: Create and Sync New Playlist**
1. Go to `/library`, select tracks
2. Click "Add to Playlist"
3. Create new playlist with "Sync to Spotify" checked
4. Verify playlist appears in Spotify app

**Test 2: Modify and Re-sync Existing Playlist**
1. Add tracks to existing playlist from `/library`
2. Go to `/playlists`
3. Verify yellow "Has Changes" badge appears
4. Click "Push Updates"
5. Verify badge changes to green "Synced"
6. Check Spotify app for updated tracks

**Test 3: Search and Filter**
1. Navigate to `/playlists`
2. Use search box to find playlists by name
3. Try each filter option
4. Verify counts are accurate

**Test 4: Playlist Detail Modal**
1. Click eye icon on any playlist
2. Verify all tracks display correctly
3. Check duration, artist, album info
4. Close modal and verify state resets

---

## Known Issues

None currently. All critical bugs have been fixed.

---

## Related Documentation

- `CLAUDE.md` - Project overview
- `ADD_TO_PLAYLIST_FEATURE.md` - Initial implementation
- `SPOTIFY_SYNC_FEATURE.md` - Sync strategy details
- `issues.md` - Tracked bugs and enhancements
