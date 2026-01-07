# Spotify PlaybackWorker - macOS Daemon Setup Complete! ✅

**Service Status:** Running  
**PID:** 27961  
**Log Location:** `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/`

---

## What's Running

Your PlaybackWorker is now running as a macOS LaunchAgent daemon:
- ✅ **Starts automatically** on login
- ✅ **Restarts automatically** if it crashes
- ✅ **Runs in background** (no terminal needed)
- ✅ **Syncs every 30 minutes** automatically
- ✅ **Tracks playback** every 10 minutes

---

## Service Management Commands

### Check Status
```bash
launchctl list | grep spotify
# Shows: PID, exit code, and service name
```

### View Logs (Real-time)
```bash
tail -f ~/Library/LaunchAgents/logs/playback-worker-*.log
# Or
tail -f /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stdout.log
```

### Stop Service
```bash
launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

### Start Service
```bash
launchctl load ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

### Restart Service
```bash
launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist
launchctl load ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

### Remove Service (Uninstall)
```bash
launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist
rm ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

---

## Current Activity

**Last Sync:** January 6, 2026 at 16:14  
**Result:** 
- 0 new tracks (already up to date)
- 892 artists synced (this is the stub enrichment backlog - will drop to ~0 on next sync with your fix!)
- 0 albums synced
- 0 playlists changed

**Next Sync:** January 6, 2026 at 16:44 (30 minutes from start)

**Next Playback Check:** Every 10 minutes (tracks what you're listening to)

---

## Log Files

### Main Application Log (Rotating)
`/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/playback-worker-YYYY-MM-DD.log`
- Rotates daily
- Kept for 30 days
- Structured JSON-like logging

### Stdout/Stderr (LaunchAgent Logs)
- `logs/stdout.log` - Standard output
- `logs/stderr.log` - Errors (should be empty if healthy)

### Quick View Recent Activity
```bash
tail -100 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/playback-worker-$(date +%Y%m%d).log
```

---

## What Happens Automatically

### Every 30 Minutes (Incremental Sync)
1. Check for new tracks added to Spotify
2. Enrich any stub artists/albums (one-time only now!)
3. Refresh stale metadata (>7 days old)
4. Check for playlist changes
5. Log results to database

### Every 10 Minutes (Playback Tracking)
1. Fetch recently played tracks from Spotify
2. Store in `play_history` table
3. Build listening analytics over time

### On Login/Boot
- Service starts automatically
- No manual intervention needed

### On Crash/Error
- Service restarts automatically after 10 seconds
- Max 3 consecutive errors logged before raising alert
- Continues attempting to recover

---

## Monitoring Your Service

### Quick Health Check
```bash
# Is it running?
launchctl list | grep spotify

# Any errors?
tail -20 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stderr.log

# Last sync result?
tail -50 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/playback-worker-$(date +%Y%m%d).log | grep "sync completed"
```

### Web UI Monitoring
Navigate to: http://localhost:5241/sync
- Real-time sync status
- Progress bars if sync is active
- History of all syncs
- Trigger manual syncs

### Database Check
```bash
psql -h localhost -p 5433 -U spotify -d spotify -c "SELECT * FROM sync_history ORDER BY started_at DESC LIMIT 5;"
```

---

## Troubleshooting

### Service Not Running
```bash
# Check logs for error
tail -50 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stderr.log

# Manually test the executable
cd /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker
./bin/Release/net8.0/SpotifyTools.PlaybackWorker
```

### Database Connection Issues
- Ensure PostgreSQL is running: `pg_isready -h localhost -p 5433`
- Check `appsettings.json` connection string
- Verify database exists: `psql -h localhost -p 5433 -U spotify -l`

### Authentication Issues
- Service uses stored refresh token (no browser needed)
- If token expired, you may need to re-authenticate via CLI or Web app
- Check logs for "Authentication failed" messages

### Too Many API Calls / Rate Limits
- Rate limiter enforces 60 requests/minute
- Incremental sync should be fast (<5 minutes typically)
- Full sync can take 30-45 minutes (normal)
- Check logs for "Rate limited" messages

---

## Performance Expectations

### Typical Incremental Sync (After Fix)
- **Duration:** 1-3 minutes
- **New tracks:** 0-10 (only if you added music)
- **Artists synced:** 0-20 (only new artists or >7 days old)
- **Albums synced:** 0-10 (only new albums or >7 days old)
- **Playlists changed:** 0-5 (only if you modified playlists)
- **API calls:** 50-200 (well under limit)

### First Sync After Stub Fix
- **Duration:** 5-10 minutes (clearing backlog)
- **Artists synced:** May be high (0-892) as it clears stubs one last time
- **After that:** Should drop to ~0-20 per sync

### Resource Usage
- **Memory:** ~100-150 MB
- **CPU:** <5% idle, 10-20% during sync
- **Disk:** Log files rotate, kept 30 days (~50 MB total)

---

## Configuration

### Change Sync Interval
Edit: `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/appsettings.json`

```json
{
  "Sync": {
    "IntervalMinutes": 30  // Change to 60, 15, etc.
  }
}
```

After changing, restart service:
```bash
launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist
launchctl load ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

### Change Playback Polling Interval
```json
{
  "PlaybackTracking": {
    "PollingIntervalMinutes": 10  // Change to 5, 30, etc.
  }
}
```

---

## Files Installed

1. **LaunchAgent Plist:**  
   `~/Library/LaunchAgents/com.spotify.playback.worker.plist`

2. **Executable:**  
   `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/bin/Release/net8.0/SpotifyTools.PlaybackWorker`

3. **Configuration:**  
   `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/appsettings.json`

4. **Logs:**  
   `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/`

---

## ✅ Setup Complete!

Your Spotify PlaybackWorker is now running as a macOS daemon. You don't need to think about it anymore - it will:
- Sync your library automatically every 30 minutes
- Track your listening history every 10 minutes
- Start on login
- Restart on failure
- Log everything for monitoring

**Next Steps:**
1. Let it run for a few hours
2. Check `/sync` page to see sync history
3. Add some music to Spotify, wait 30 minutes, see it auto-sync
4. Enjoy not having to manually sync ever again! 🎉
