# Current Status - January 6, 2026 (16:33 UTC)

## Session Summary

**Work Completed:**
1. ✅ Implemented Add-to-Playlist modal component
2. ✅ Added Spotify playlist sync functionality
3. ⚠️ **BUGS DISCOVERED** - Playlist creation has critical issues

---

## Active Bug - CRITICAL

### Symptom:
Creating a new playlist with tracks fails with error:
```
System.InvalidOperationException: Playlist 'drift phonk' has no tracks to sync.
at SpotifyTools.Web.Services.PlaylistService.SyncPlaylistToSpotifyAsync(String playlistId)
```

### Root Cause:
**Race condition / ordering issue** in `CreateAndSyncPlaylistAsync()`:

```csharp
// Current (BROKEN) flow in AddToPlaylistModal.razor:
1. Create playlist locally (gets GUID)
2. Add tracks to playlist (using GUID)
3. Sync to Spotify (creates on Spotify, gets Spotify ID)
4. Delete local GUID record
5. Create new record with Spotify ID
6. ❌ Recreate track relationships with Spotify ID
```

**Problem:** Step 6 happens INSIDE `SyncPlaylistToSpotifyAsync()`, but step 2 happens BEFORE sync is called. When sync runs, it queries for tracks but the relationships haven't been moved to the new Spotify ID yet.

### Where the Bug Lives:
- **File:** `src/SpotifyTools.Web/Services/PlaylistService.cs`
- **Method:** `CreateAndSyncPlaylistAsync()` (lines 268-285)
- **Root Issue:** Method calls `CreatePlaylistAsync()` then `AddTracksToPlaylistAsync()` then `SyncPlaylistToSpotifyAsync()`, but sync expects tracks to exist before it runs

### Flow Issue in Detail:

**AddToPlaylistModal.razor (CreateAndAddToPlaylist method):**
```csharp
// Line ~248-280
if (syncToSpotify)
{
    newPlaylist = await PlaylistService.CreateAndSyncPlaylistAsync(request);
    // At this point, tracks haven't been added yet!
}

// Add tracks AFTER creation
await PlaylistService.AddTracksToPlaylistAsync(newPlaylist.Id, TrackIds);
```

**PlaylistService.cs (CreateAndSyncPlaylistAsync):**
```csharp
// Line 268-285
public async Task<PlaylistDto> CreateAndSyncPlaylistAsync(CreatePlaylistRequest request)
{
    // Creates local playlist (GUID)
    var playlistDto = await CreatePlaylistAsync(request);

    // Tries to sync, but NO TRACKS exist yet!
    var spotifyPlaylistId = await SyncPlaylistToSpotifyAsync(playlistDto.Id);
    
    playlistDto.SpotifyId = spotifyPlaylistId;
    return playlistDto;
}
```

**PlaylistService.cs (SyncPlaylistToSpotifyAsync):**
```csharp
// Line 339 - FAILS HERE
var playlistTracks = await _dbContext.PlaylistTracks
    .Where(pt => pt.PlaylistId == playlistId)
    .OrderBy(pt => pt.Position)
    .Select(pt => pt.TrackId)
    .ToListAsync();

if (!playlistTracks.Any())
{
    throw new InvalidOperationException($"Playlist '{playlist.Name}' has no tracks to sync.");
}
```

---

## Required Fix

### Option 1: Change Method Signature (RECOMMENDED)
Add trackIds parameter to sync methods:

```csharp
// IPlaylistService.cs
Task<PlaylistDto> CreateAndSyncPlaylistAsync(CreatePlaylistRequest request, List<string> trackIds);

// PlaylistService.cs implementation
public async Task<PlaylistDto> CreateAndSyncPlaylistAsync(CreatePlaylistRequest request, List<string> trackIds)
{
    // Create local playlist
    var playlistDto = await CreatePlaylistAsync(request);
    
    // Add tracks FIRST
    await AddTracksToPlaylistAsync(playlistDto.Id, trackIds);
    
    // Now sync (tracks exist)
    var spotifyPlaylistId = await SyncPlaylistToSpotifyAsync(playlistDto.Id);
    
    playlistDto.SpotifyId = spotifyPlaylistId;
    return playlistDto;
}

// AddToPlaylistModal.razor - UPDATE CALL
if (syncToSpotify)
{
    newPlaylist = await PlaylistService.CreateAndSyncPlaylistAsync(request, TrackIds);
    // Don't call AddTracksToPlaylistAsync again!
}
else
{
    newPlaylist = await PlaylistService.CreatePlaylistAsync(request);
    await PlaylistService.AddTracksToPlaylistAsync(newPlaylist.Id, TrackIds);
}
```

### Option 2: Reorder Operations in Modal
Move track addition inside the sync method (more complex, not recommended)

### Option 3: Make Sync Allow Empty Playlists
Remove the empty check, create empty playlist, then add tracks separately (creates playlist twice - wasteful)

---

## Additional Issues to Investigate

1. **ID Migration Bug:** When syncing replaces GUID with Spotify ID, the old PlaylistTrack records are deleted and recreated. This might not preserve Position correctly.

2. **Transaction Safety:** The delete-and-recreate pattern in `SyncPlaylistToSpotifyAsync()` is not atomic. If it fails midway, data could be lost.

3. **Duplicate Check in Modal:** The modal fetches playlist details to check for duplicates BEFORE adding. With the new ID migration, the playlist ID changes after sync, so references might break.

---

## Files Modified in This Session

### Created:
- `src/SpotifyTools.Web/Components/AddToPlaylistModal.razor` - Modal component (HAS BUGS)

### Updated:
- `src/SpotifyTools.Web/Services/IPlaylistService.cs` - Added sync methods
- `src/SpotifyTools.Web/Services/PlaylistService.cs` - Implemented sync (BUGGY)
- `src/SpotifyTools.Web/Pages/Home.razor` - Integrated modal
- `src/SpotifyTools.Web/Components/_Imports.razor` - Added service imports
- `src/SpotifyTools.Web/appsettings.json` - Updated Spotify credentials

### Documentation:
- `ADD_TO_PLAYLIST_FEATURE.md` - Feature documentation (needs update for bugs)
- `SPOTIFY_SYNC_FEATURE.md` - Sync documentation (needs update for bugs)

---

## Testing Status

✅ Build succeeds  
✅ Modal opens and displays playlists  
✅ Spotify credentials configured  
❌ **Creating playlist with sync FAILS** (no tracks error)  
⏳ Creating playlist without sync - UNTESTED  
⏳ Adding to existing playlist - UNTESTED  
⏳ Duplicate detection - UNTESTED  

---

## Quick Start for Next Session

### Immediate Action Required:
1. Fix the race condition in `CreateAndSyncPlaylistAsync()`
2. Update modal to pass trackIds to sync method
3. Test full flow: create → add tracks → sync → verify in Spotify
4. Add transaction safety around ID migration

### Commands to Run:
```bash
# Web app is running on session 68
cd /Users/bretthardman/_dev/spotify_tools
dotnet run --project src/SpotifyTools.Web/SpotifyTools.Web.csproj

# Access at: http://localhost:5241/library
```

### Files to Edit:
1. `src/SpotifyTools.Web/Services/IPlaylistService.cs` - Add trackIds param
2. `src/SpotifyTools.Web/Services/PlaylistService.cs` - Fix CreateAndSyncPlaylistAsync
3. `src/SpotifyTools.Web/Components/AddToPlaylistModal.razor` - Update method call

### Test Plan:
1. Select 5-10 tracks from a genre
2. Click "Add to Playlist"
3. Create new playlist with "Sync to Spotify" checked
4. Should successfully create AND sync
5. Open Spotify app to verify playlist exists with tracks

---

## Context Files to Read

- `CLAUDE.md` - Project overview and architecture
- `ADD_TO_PLAYLIST_FEATURE.md` - What was attempted (has bugs)
- `SPOTIFY_SYNC_FEATURE.md` - Sync implementation details
- This file (`CURRENT_STATUS.md`) - Current state and bugs

---

## Notes

- User is tired and stopping for the day
- Main functionality is 90% complete
- Just needs the ordering bug fixed
- Once fixed, should be a quick test-and-ship

**Status:** 🔴 BLOCKED - Critical bug in playlist creation flow
**Next Agent:** Start by fixing the race condition in CreateAndSyncPlaylistAsync
