# Incremental Sync Artist/Album Stub Detection Fix

**Date:** January 6, 2026  
**Issue:** Incremental sync incorrectly re-syncing hundreds of artists/albums every time

---

## Problem Description

### Observed Behavior
User reported 800+ artists being synced during every incremental sync, despite only doing a full sync 1-2 days prior.

### Root Cause
The stub detection logic was using **field presence** to identify stubs:

**Artists (OLD - INCORRECT):**
```csharp
// Sync if it's a stub (no genres)
if (artist.Genres.Length == 0)
    return true;
```

**Albums (OLD - INCORRECT):**
```csharp
// Sync if it's a stub (no label)
if (string.IsNullOrEmpty(album.Label))
    return true;
```

### Why This Was Wrong

1. **Many artists legitimately have NO genres** in Spotify's database:
   - Newer/emerging artists
   - Classical/orchestral artists
   - Niche genres not cataloged by Spotify
   - Artists from certain regions

2. **Many albums legitimately have NO label** (or blank label):
   - Self-published albums
   - Independent releases
   - Certain distribution models

3. **Playlist tracks add many more artists:**
   - Full sync now captures ALL playlist tracks (not just saved tracks)
   - Spotify's automatic playlists contain many artists user hasn't explicitly saved
   - These artists often have sparse metadata (no genres, no labels)

4. **False positive cycle:**
   - Artist synced with 0 genres → Spotify returns 0 genres → Still considered stub
   - Next incremental sync → Still 0 genres → Synced again → Counted as "added"
   - Repeats infinitely, wasting API calls and showing inflated "added" counts

---

## Solution Implemented

### New Stub Detection Logic

Use **sync timestamps** instead of field presence to identify true stubs:

**Artists (NEW - CORRECT):**
```csharp
// Sync if it's a stub (never been fully synced)
// Note: Many artists legitimately have no genres, so don't use Genres.Length == 0
if (artist.LastSyncedAt == default || artist.LastSyncedAt == artist.FirstSyncedAt)
    return true;
```

**Albums (NEW - CORRECT):**
```csharp
// Sync if it's a stub (never been fully synced)
// Note: Some albums legitimately have no label, so don't rely on Label field
if (album.LastSyncedAt == default || album.LastSyncedAt == album.FirstSyncedAt)
    return true;
```

### Logic Explanation

**`LastSyncedAt == default`:**
- Artist/album record exists but was never synced (shouldn't happen, but handles edge case)

**`LastSyncedAt == FirstSyncedAt`:**
- Artist/album was created but never enriched with full metadata
- This is a TRUE stub: created during track sync, needs one-time enrichment

**After first enrichment:**
- `LastSyncedAt` updates to current time (different from `FirstSyncedAt`)
- Artist/album no longer considered a stub
- Only re-synced if metadata becomes stale (7 days old)

### Stub Counting Update

Also updated the counting logic to match:

**OLD:**
```csharp
var wasStub = existingArtist?.Genres.Length == 0;
var wasStub = string.IsNullOrEmpty(existingAlbum?.Label);
```

**NEW:**
```csharp
var wasStub = existingArtist?.LastSyncedAt == default || 
              existingArtist?.LastSyncedAt == existingArtist?.FirstSyncedAt;

var wasStub = existingAlbum?.LastSyncedAt == default || 
              existingAlbum?.LastSyncedAt == existingAlbum?.FirstSyncedAt;
```

---

## Expected Impact

### Before Fix
- **Incremental sync:** 800+ artists, 500+ albums every time
- **Reason:** Artists/albums with no genres/labels re-synced infinitely
- **API calls:** Wasted on re-fetching artists that won't change
- **User experience:** Confusing "added" counts that don't reflect reality

### After Fix
- **First incremental sync after full sync:** ~0-50 true stubs (legitimate stubs from interrupted syncs)
- **Subsequent incremental syncs:** Only new tracks' artists/albums
- **API calls:** Saved for artists/albums that actually need updates
- **User experience:** Accurate counts reflecting actual new additions

### Example Scenario

**User's library:**
- 1000 artists total
- 300 artists with 0 genres (legitimately no genres in Spotify)
- Last full sync: 1 day ago

**OLD behavior:**
- Incremental sync: Re-fetch all 300 zero-genre artists
- Result: "300 artists added" (misleading)

**NEW behavior:**
- Incremental sync: Skip the 300 zero-genre artists (already fully synced)
- Only sync: New tracks' artists OR artists >7 days old
- Result: "5 artists added" (accurate)

---

## Files Modified

**File:** `src/SpotifyTools.Sync/SyncService.cs`

**Changes:**
1. `IncrementalSyncArtistsAsync()` - Lines 789-806 (stub detection logic)
2. `IncrementalSyncArtistsAsync()` - Lines 825-828 (stub counting logic)
3. `IncrementalSyncAlbumsAsync()` - Lines 996-1013 (stub detection logic)
4. `IncrementalSyncAlbumsAsync()` - Lines 1032-1035 (stub counting logic)

**Lines Changed:** ~20 lines total

---

## Testing Recommendations

1. **Run incremental sync immediately after this fix:**
   - Should see drastically reduced artist/album counts
   - Only true stubs (never-synced records) should be processed

2. **Add a few new tracks to Spotify, run incremental sync:**
   - Should only sync artists/albums from those new tracks
   - Should NOT re-sync existing artists with 0 genres

3. **Wait 8+ days, run incremental sync:**
   - Should sync artists/albums with stale metadata (>7 days old)
   - This is expected behavior for metadata refresh

4. **Check sync history:**
   - "Artists added" should be realistic (single digits typically)
   - Not hundreds like before

---

## Related Constants

**METADATA_REFRESH_DAYS = 7** (defined in SyncService.cs)
- Artists/albums are considered "stale" after 7 days
- Stale records get metadata refreshed (popularity, follower counts, etc.)
- This is separate from stub detection

---

## Database Impact

**No migration needed** - this is a logic-only fix.

The database schema already has:
- `first_synced_at` timestamp
- `last_synced_at` timestamp

We're just using these fields correctly now.

---

## Future Enhancements

### Configuration
Could make metadata refresh threshold configurable:
```json
{
  "Sync": {
    "MetadataRefreshDays": 7,
    "EnableStubEnrichment": true
  }
}
```

### Metrics
Could track and display:
- True stubs enriched
- Stale metadata refreshed
- Already up-to-date (skipped)

### Smart Refresh
Could prioritize refreshing:
- High-popularity artists (more likely to change)
- Recently released albums (metadata may update)
- Artists user plays frequently

---

## ✅ Fix Complete

Build successful. Ready to test with next incremental sync.

**Expected result:** Drastically reduced artist/album sync counts, more accurate "added" metrics.
