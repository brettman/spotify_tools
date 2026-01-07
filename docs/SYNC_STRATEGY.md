# Incremental Sync Strategy

**Status: Phase 1 COMPLETE** (January 6, 2026)

## Overview

This document outlines the strategy for implementing resilient, resumable background syncing that can handle Spotify API rate limits gracefully.

### ✅ Phase 1 Complete: Batched Sync with Checkpointing

**Implemented Components:**
1. **BatchSyncResult Model** - Tracks progress, errors, rate limits
2. **Batched Sync Methods** - 4 methods in SyncService (tracks, artists, albums, playlists)
3. **IncrementalSyncOrchestrator** - Coordinates phases with checkpointing
4. **SyncState Persistence** - Database checkpoints per sync phase

**Key Features:**
- ✅ Checkpoint persistence after each batch
- ✅ Automatic rate limit detection and waiting
- ✅ Progress tracking with callbacks
- ✅ Cancellation support
- ✅ Error recovery
- ✅ Resume from last checkpoint on restart

---

## Problem Statement

### Current Issues
1. **All-or-Nothing Sync** - Full sync must complete in one session or fail entirely
2. **Rate Limit Failures** - Hitting daily Spotify API limits causes sync to fail (takes days to complete manually)
3. **No Progress Persistence** - Progress isn't saved between sync attempts
4. **Poor User Experience** - New users must babysit the CLI, manually restart after hitting limits
5. **Manual Dependency** - Requires running CLI app to sync, separate from playback tracking

### Goals
- **Set-and-Forget:** New user installs daemon, it handles everything in background
- **Rate Limit Resilience:** Gracefully wait 24 hours when rate limited, then resume
- **Progress Persistence:** Save progress after each batch, resume from checkpoint
- **Continuous Operation:** Never crash or give up, always making forward progress
- **Visibility:** Web UI shows sync status, progress, and estimated completion

---

## Architecture

### Entity Sync Priority

**Priority 1: Tracks** (what user listens to)
- Sync saved library tracks first
- Enables basic functionality (track browsing, play history matching)

**Priority 2: Artists** (enrichment)
- Fetch artist details for all tracks
- Enables genre filtering, artist browsing

**Priority 3: Albums** (enrichment)
- Fetch album details for all tracks
- Enables album browsing, release date filters

**Priority 4: Playlists** (user organization)
- Fetch user playlists and their tracks
- May discover tracks not in saved library

### Sync Modes

#### Initial Full Sync (New User)
- **Batch Size:** 200 entities per batch (maximize throughput)
- **Frequency:** Continuous (as fast as rate limits allow)
- **Checkpointing:** Save progress after each batch
- **Priority Order:** Tracks → Artists → Albums → Playlists
- **Goal:** Get to "usable state" (all tracks synced) ASAP

#### Incremental Sync (Ongoing)
- **Batch Size:** 50 entities per batch (lighter, more responsive)
- **Frequency:** Every 6 hours (or after user activity)
- **Purpose:** Catch new saves, updated metadata, new playlists
- **Strategy:** Fetch recently changed entities only

#### Playback Tracking
- **Frequency:** Every 30 minutes (reduced from 10)
- **API Calls:** 1-2 per poll (very light)
- **Priority:** Continues even during rate limit waits
- **Independence:** Runs in parallel with sync operations

---

## Data Model

### New Table: `sync_state`

Tracks progress of ongoing sync operations with resumable checkpoints.

```sql
CREATE TABLE sync_state (
    id SERIAL PRIMARY KEY,
    entity_type VARCHAR(50) NOT NULL,  -- 'tracks', 'artists', 'albums', 'playlists'
    phase VARCHAR(50) NOT NULL,        -- 'initial_sync', 'incremental_sync'
    
    -- Progress tracking
    last_synced_offset INT DEFAULT 0,
    total_estimated INT DEFAULT 0,
    is_complete BOOLEAN DEFAULT false,
    
    -- Rate limit tracking
    rate_limit_hit_at TIMESTAMP NULL,
    rate_limit_reset_at TIMESTAMP NULL,
    rate_limit_remaining INT DEFAULT 0,
    
    -- Metadata
    started_at TIMESTAMP NOT NULL,
    last_updated_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP NULL,
    error_message TEXT NULL,
    
    UNIQUE(entity_type, phase)
);
```

### Expanded: `sync_history`

Add new columns to track batch-level operations:

```sql
ALTER TABLE sync_history ADD COLUMN batch_number INT DEFAULT 1;
ALTER TABLE sync_history ADD COLUMN checkpoint_offset INT DEFAULT 0;
ALTER TABLE sync_history ADD COLUMN rate_limited BOOLEAN DEFAULT false;
```

---

## Implementation Phases

### Phase 1: Incremental Sync Engine ✅ (Current)

**Goal:** Create resumable sync logic with checkpointing

**Tasks:**
1. Create `sync_state` table migration
2. Create `SyncStateRepository` 
3. Refactor `SyncService` to support batched operations:
   - `SyncTracksBatchAsync(offset, batchSize)` 
   - `SyncArtistsBatchAsync(offset, batchSize)`
   - `SyncAlbumsBatchAsync(offset, batchSize)`
   - `SyncPlaylistsBatchAsync(offset, batchSize)`
4. Add checkpoint save after each batch
5. Add resume-from-checkpoint logic
6. Unit tests for batch operations

**Deliverable:** SyncService can sync 200 tracks, save checkpoint, resume from checkpoint

---

### Phase 2: Rate Limit Handling

**Goal:** Gracefully handle 429 responses, wait, and resume

**Tasks:**
1. Add rate limit detection in `ExecuteWithRetryAsync`
2. Parse `Retry-After` header or calculate 24-hour wait
3. Save rate limit state to `sync_state` table
4. Add `IsRateLimited()` check before sync operations
5. Log waiting period prominently
6. Resume sync after wait period expires

**Deliverable:** Sync pauses on 429, waits 24 hours, resumes automatically

---

### Phase 3: Daemon Integration

**Goal:** Run incremental sync in background alongside playback tracking

**Tasks:**
1. Add `BackgroundSyncService` to PlaybackWorker
2. Separate sync loop from playback loop
3. Configuration for batch size, frequency
4. Ensure playback tracking continues during rate limit waits
5. Add graceful shutdown (save checkpoint on stop)

**Deliverable:** Daemon runs both sync and playback tracking in parallel

---

### Phase 4: Web UI Status Page

**Goal:** Real-time visibility into sync progress

**Tasks:**
1. Create `SyncController` with `/api/sync/status` endpoint
2. Return: current phase, progress %, rate limit status, ETA
3. Create `/sync-status` page in Blazor UI
4. Real-time updates via polling (every 5 seconds)
5. Add manual controls (pause/resume/reset)

**Deliverable:** User can monitor sync progress in web browser

---

### Phase 5: Testing & Optimization

**Goal:** Ensure reliability and performance

**Tasks:**
1. Test new user experience (empty database)
2. Simulate rate limit scenarios
3. Test daemon restart mid-sync (resume works)
4. Performance profiling (batch size optimization)
5. Documentation updates

**Deliverable:** Production-ready incremental sync system

---

## Rate Limit Strategy

### Detection
- Catch `APITooManyRequestsException` (429)
- Check response headers: `Retry-After`, `X-RateLimit-Remaining`
- Fall back to 24-hour wait if headers missing

### Response
1. **Save State:** Write current offset to `sync_state.last_synced_offset`
2. **Mark Limited:** Set `rate_limit_hit_at` and `rate_limit_reset_at`
3. **Pause Sync:** Stop all sync operations (tracks, artists, albums, playlists)
4. **Continue Tracking:** Playback tracking keeps running (uses different API calls)
5. **Log Clearly:**
   ```
   ⚠️  Spotify API rate limit hit at 2026-01-06 08:00 UTC
   ⏸️  Sync paused. Will resume at 2026-01-07 08:00 UTC
   ✅ Playback tracking continues normally
   📊 Progress saved: 1,247 of 3,500 tracks synced
   ```

### Recovery
1. **Check on Startup:** If `rate_limit_reset_at` is in past, clear rate limit state
2. **Resume Sync:** Continue from `last_synced_offset`
3. **Log Resume:**
   ```
   ✅ Rate limit wait complete
   🔄 Resuming sync from checkpoint: 1,247 tracks
   ```

---

## Batch Sizes & Frequencies

### Initial Full Sync
- **Tracks:** 200 per batch
- **Artists:** 200 per batch  
- **Albums:** 200 per batch
- **Playlists:** 50 per batch (includes fetching all tracks per playlist)
- **Frequency:** Continuous (loop immediately after batch completes)
- **Checkpoint:** After every batch

### Incremental Sync (After Initial Complete)
- **All Entities:** 50 per batch
- **Frequency:** Every 6 hours
- **Strategy:** Only fetch items with `last_synced_at` > 7 days ago OR new items
- **Checkpoint:** After every batch

### Playback Tracking
- **Frequency:** Every 30 minutes (reduced to be less aggressive)
- **API Calls:** 1 call per poll (50 tracks max)
- **Independence:** Unaffected by sync rate limits

---

## Configuration

### appsettings.json

```json
{
  "Sync": {
    "InitialBatchSize": 200,
    "IncrementalBatchSize": 50,
    "IncrementalSyncIntervalHours": 6,
    "EnableAutoResume": true,
    "CheckpointFrequency": "EveryBatch"  // or "Every10Batches"
  },
  "PlaybackTracking": {
    "PollingIntervalMinutes": 30,  // Changed from 10
    "EnableErrorAlerts": true,
    "MaxConsecutiveErrors": 5
  }
}
```

---

## User Experience Flow

### New User (Day 1)
```
08:00 - User installs daemon, configures DB, starts service
08:01 - Browser opens for OAuth, user authorizes
08:02 - Daemon starts: "Beginning initial library sync..."
08:03 - Syncs 200 tracks (batch 1)
08:04 - Syncs 200 tracks (batch 2)
...
10:00 - Rate limit hit after 1,000 tracks
        "⏸️  Paused. Resuming tomorrow at 10:00 AM"
        "✅ 1,000 of 3,500 tracks synced (29%)"
```

### New User (Day 2)
```
10:01 - Daemon automatically resumes
        "✅ Rate limit reset. Resuming from checkpoint..."
10:02 - Syncs batch 6 (1,001-1,200)
...
12:00 - Rate limit hit again at 2,000 tracks
```

### New User (Day 4)
```
All tracks synced ✅
Syncing artists (batch 1/15)...
```

### Existing User (Ongoing)
```
Every 30 minutes: Playback tracking polls
Every 6 hours: Incremental sync checks for new/updated items
No interruption to user
```

---

## Success Metrics

### Quantitative
- **Time to First Track:** < 5 minutes (first 200 tracks synced)
- **Time to Usable State:** < 3 days (all tracks synced, even with rate limits)
- **Uptime:** 99.9% (daemon doesn't crash)
- **Progress Loss:** 0% (checkpointing works)

### Qualitative
- User can start using Web UI immediately (even with partial data)
- User doesn't need to monitor or intervene
- Clear visibility into progress and status
- Confidence that sync will eventually complete

---

## Open Questions

### Q: Should we parallelize entity syncs?
**Answer:** No, sequential is better for rate limit management. Once tracks are done, move to artists, etc.

### Q: What if user adds 500 new tracks during a sync?
**Answer:** Incremental sync will catch them on next run (6 hours). Initial sync focuses on snapshot at start time.

### Q: How to handle deleted tracks/playlists?
**Answer:** Incremental sync should mark as deleted (soft delete) if no longer in user's library.

### Q: Should sync be pausable/resumable via Web UI?
**Answer:** Phase 4 feature. Add manual pause/resume/reset controls.

---

## Version History

- **v1.0** (2026-01-06): Initial strategy document
- **Status:** Phase 1 in progress

---

## Related Documents

- `BACKUP.md` - Database backup strategy
- `src/SpotifyTools.PlaybackWorker/README.md` - Daemon documentation
- `WebUIArchitecture.md` - Web UI design
