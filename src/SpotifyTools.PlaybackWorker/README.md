# Spotify Playback Worker Service

A standalone background service that continuously tracks your Spotify listening history.

## Features

- **Autonomous Operation**: Runs independently as a system daemon/service
- **Self-Authenticating**: Automatically handles Spotify OAuth on first run
- **Automatic Polling**: Fetches recently played tracks every 10 minutes (configurable)
- **Token Management**: Stores and refreshes OAuth tokens automatically
- **Error Handling**: Tracks consecutive errors and raises alerts after threshold
- **Persistent Logging**: Logs to both console and rotating log files
- **Self-Recovery**: Automatically restarts on failure with configurable restart policy

## Prerequisites

1. **PostgreSQL**: Database must be running and accessible
2. **.NET 8 Runtime**: Required to run the service
3. **Spotify Developer App**: ClientId and ClientSecret configured in appsettings.json

**Note:** No manual authentication required! The service will authenticate itself on first run.

## Configuration

Edit `appsettings.json` to configure:

```json
{
  "ConnectionStrings": {
    "SpotifyDatabase": "Host=localhost;Port=5433;Database=spotify_tools;..."
  },
  "Spotify": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "RedirectUri": "http://127.0.0.1:5009/callback"
  },
  "PlaybackTracking": {
    "PollingIntervalMinutes": 10,
    "EnableErrorAlerts": true,
    "MaxConsecutiveErrors": 5
  }
}
```

## First Run - Authentication

**On the very first run**, the service will:

1. Detect that no OAuth token exists
2. Print authentication instructions to console/log
3. Open your default browser automatically
4. Wait for you to authorize the application
5. Save the refresh token to the database
6. Continue running normally

**On subsequent runs**, the service uses the stored refresh token automatically - no browser required!

### Interactive Authentication Example

```
═══════════════════════════════════════════════════════════
  SPOTIFY AUTHENTICATION REQUIRED
═══════════════════════════════════════════════════════════

This service needs to authenticate with Spotify.
A browser window will open automatically.

✓ Authentication successful!
✓ Refresh token saved to database
✓ Future runs will authenticate automatically

═══════════════════════════════════════════════════════════
```

## Installation

### Linux (systemd)

1. **Build the project in Release mode:**
   ```bash
   dotnet publish -c Release
   ```

2. **Update the service file** (`spotify-playback-worker.service`):
   - Replace `YOUR_USERNAME` with your actual username
   - Update paths to match your installation directory

3. **Copy the service file:**
   ```bash
   sudo cp spotify-playback-worker.service /etc/systemd/system/
   ```

4. **Enable and start the service:**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable spotify-playback-worker
   sudo systemctl start spotify-playback-worker
   ```

5. **Check status:**
   ```bash
   sudo systemctl status spotify-playback-worker
   ```

6. **View logs:**
   ```bash
   sudo journalctl -u spotify-playback-worker -f
   ```

### macOS (launchd)

1. **Build the project:**
   ```bash
   dotnet publish -c Release
   ```

2. **Create a launchd plist file** at `~/Library/LaunchAgents/com.spotify.playback.worker.plist`:
   ```xml
   <?xml version="1.0" encoding="UTF-8"?>
   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
   <plist version="1.0">
   <dict>
       <key>Label</key>
       <string>com.spotify.playback.worker</string>
       <key>ProgramArguments</key>
       <array>
           <string>/Users/YOUR_USERNAME/spotify_tools/src/SpotifyTools.PlaybackWorker/bin/Release/net8.0/SpotifyTools.PlaybackWorker</string>
       </array>
       <key>WorkingDirectory</key>
       <string>/Users/YOUR_USERNAME/spotify_tools/src/SpotifyTools.PlaybackWorker</string>
       <key>RunAtLoad</key>
       <true/>
       <key>KeepAlive</key>
       <true/>
       <key>StandardOutPath</key>
       <string>/Users/YOUR_USERNAME/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stdout.log</string>
       <key>StandardErrorPath</key>
       <string>/Users/YOUR_USERNAME/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stderr.log</string>
   </dict>
   </plist>
   ```

3. **Load the service:**
   ```bash
   launchctl load ~/Library/LaunchAgents/com.spotify.playback.worker.plist
   ```

4. **Check status:**
   ```bash
   launchctl list | grep spotify
   ```

5. **View logs:**
   ```bash
   tail -f logs/playback-worker-*.log
   ```

### Windows Service

1. **Build the project:**
   ```powershell
   dotnet publish -c Release
   ```

2. **Install as Windows Service** (using sc.exe or NSSM):
   ```powershell
   sc.exe create "SpotifyPlaybackWorker" binPath="C:\path\to\SpotifyTools.PlaybackWorker.exe"
   sc.exe start "SpotifyPlaybackWorker"
   ```

## Running Manually (Development/Testing)

```bash
dotnet run
```

Or with specific configuration:
```bash
dotnet run --environment Development
```

## Logs

Logs are written to:
- **Console**: Real-time output
- **File**: `logs/playback-worker-YYYY-MM-DD.log` (rotated daily, kept for 30 days)

## Monitoring

### Check if running:
```bash
# Linux
sudo systemctl status spotify-playback-worker

# macOS
launchctl list | grep spotify

# Manual process check
ps aux | grep SpotifyTools.PlaybackWorker
```

### View recent logs:
```bash
# Linux (systemd)
sudo journalctl -u spotify-playback-worker -n 100

# macOS/Manual
tail -f logs/playback-worker-*.log
```

## Troubleshooting

### Service won't start
1. Check that PostgreSQL is running
2. Verify database connection string in `appsettings.json`
3. Ensure you've authenticated via CLI or Web app first
4. Check logs for specific errors

### Not tracking plays
1. Verify Spotify authentication is valid
2. Check that polling interval has elapsed
3. Ensure you're actively listening to music on Spotify
4. Check database for recent entries: `SELECT * FROM play_history ORDER BY played_at DESC LIMIT 10;`

### Too many errors
1. Check Spotify API rate limits
2. Verify network connectivity
3. Check database connection
4. Review logs for specific error messages

## Uninstalling

### Linux
```bash
sudo systemctl stop spotify-playback-worker
sudo systemctl disable spotify-playback-worker
sudo rm /etc/systemd/system/spotify-playback-worker.service
sudo systemctl daemon-reload
```

### macOS
```bash
launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist
rm ~/Library/LaunchAgents/com.spotify.playback.worker.plist
```

## Alert Configuration

The service tracks consecutive errors and can raise alerts when threshold is exceeded.

Current alert mechanisms:
- **Logging**: Critical errors logged to file and console
- **TODO**: Email notifications (SMTP)
- **TODO**: Slack webhooks
- **TODO**: System notifications

To extend alerting, modify the `RaiseErrorAlert` method in `PlaybackTracker.cs`.
