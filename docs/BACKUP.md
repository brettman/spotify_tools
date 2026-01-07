# Database Backup & Restore Guide

## 📦 Backup System Overview

### What Gets Backed Up
- **Database:** PostgreSQL `spotify_tools` (all tables, data, indexes)
- **Size:** ~1.5 MB compressed
- **Frequency:** Daily at 2:00 AM (automatic)
- **Retention:** 30 days (older backups auto-deleted)

### Backup Locations
- **Backups:** `~/spotify-backups/`
- **Script:** `~/backup-spotify-db.sh`
- **Log:** `~/spotify-backups/backup.log`

### Backup Format
- **Files:** `backup-YYYYMMDD-HHMMSS.dump`
- **Example:** `backup-20260105-020000.dump`
- **Format:** PostgreSQL custom format (compressed, binary)

---

## 🔄 How Backups Work

### Automatic Backups (Cron)
```bash
# Runs daily at 2:00 AM via cron
# No downtime - database stays running
# PlaybackTrackingService continues to work

# Check cron schedule:
crontab -l | grep spotify
```

### What Happens During Backup
1. Script runs at 2 AM
2. pg_dump creates snapshot (takes ~1 second)
3. File copied from Docker container to Mac
4. Backup logged to `backup.log`
5. Old backups (>30 days) deleted automatically
6. Database keeps running - zero downtime

---

## 🧪 Manual Backup (Anytime)

```bash
# Run backup manually
~/backup-spotify-db.sh

# Check if it worked
ls -lh ~/spotify-backups/ | tail -5
cat ~/spotify-backups/backup.log | tail -5
```

---

## 🔧 Restore Instructions

### Scenario 1: Full Database Restore (Nuclear Option)

**⚠️ WARNING: This deletes ALL current data!**

```bash
# 1. Stop the application
cd ~/spotify_tools
docker-compose down

# 2. Start only the database
docker-compose up -d postgres

# 3. Drop and recreate database
docker exec spotify-tools-db dropdb -U spotify_user spotify_tools
docker exec spotify-tools-db createdb -U spotify_user spotify_tools

# 4. Copy backup file into container
docker cp ~/spotify-backups/backup-YYYYMMDD-HHMMSS.dump \
  spotify-tools-db:/tmp/restore.dump

# 5. Restore the backup
docker exec spotify-tools-db pg_restore -U spotify_user \
  -d spotify_tools -v /tmp/restore.dump

# 6. Clean up and restart
docker exec spotify-tools-db rm /tmp/restore.dump
docker-compose up -d
```

### Scenario 2: Restore Specific Tables Only

```bash
# List what's in a backup
docker cp ~/spotify-backups/backup-YYYYMMDD-HHMMSS.dump \
  spotify-tools-db:/tmp/backup.dump

docker exec spotify-tools-db pg_restore -U spotify_user \
  --list /tmp/backup.dump

# Restore only play_history table (example)
docker exec spotify-tools-db pg_restore -U spotify_user \
  -d spotify_tools -t play_history /tmp/backup.dump
```

### Scenario 3: Restore to Test Database (Safe Testing)

```bash
# Create test database
docker exec spotify-tools-db createdb -U spotify_user spotify_test

# Restore backup to test database
docker cp ~/spotify-backups/backup-YYYYMMDD-HHMMSS.dump \
  spotify-tools-db:/tmp/test.dump

docker exec spotify-tools-db pg_restore -U spotify_user \
  -d spotify_test /tmp/test.dump

# Inspect test database
docker exec spotify-tools-db psql -U spotify_user -d spotify_test -c "\dt"

# Drop test database when done
docker exec spotify-tools-db dropdb -U spotify_user spotify_test
```

---

## 📊 Monitoring Backups

### Check Recent Backups
```bash
# List all backups
ls -lh ~/spotify-backups/

# Show last 10 backups
ls -lt ~/spotify-backups/backup-*.dump | head -10

# Check total backup storage
du -sh ~/spotify-backups/
```

### Check Backup Log
```bash
# View recent backup activity
tail -20 ~/spotify-backups/backup.log

# Check for failures
grep "FAILED" ~/spotify-backups/backup.log

# Count successful backups
grep "✅ Backup completed" ~/spotify-backups/backup.log | wc -l
```

### Verify a Backup is Valid
```bash
# Test that backup file isn't corrupted
BACKUP_FILE=~/spotify-backups/backup-20260105-020000.dump

docker cp $BACKUP_FILE spotify-tools-db:/tmp/test-verify.dump

docker exec spotify-tools-db pg_restore --list /tmp/test-verify.dump | head -20

# If you see table listings, backup is valid
# Clean up
docker exec spotify-tools-db rm /tmp/test-verify.dump
```

---

## 🚨 Troubleshooting

### Backup Script Not Running
```bash
# Check if cron job exists
crontab -l | grep spotify

# Check if script is executable
ls -l ~/backup-spotify-db.sh
# Should show: -rwxr-xr-x

# Make executable if needed
chmod +x ~/backup-spotify-db.sh

# Test script manually
~/backup-spotify-db.sh
```

### Database Connection Errors
```bash
# Check if container is running
docker ps | grep spotify-tools-db

# Check container logs
docker logs spotify-tools-db | tail -50

# Restart database if needed
cd ~/spotify_tools
docker-compose restart postgres
```

### Backup Files Not Found
```bash
# Check backup directory exists
ls -la ~/spotify-backups/

# Check cron is running (on macOS, may need to grant Terminal Full Disk Access)
# System Preferences → Security & Privacy → Full Disk Access → Add Terminal

# Check cron service (macOS)
sudo launchctl list | grep cron
```

### Restore Fails
```bash
# Common error: Database has existing data
# Solution: Use --clean flag to drop existing objects first
docker exec spotify-tools-db pg_restore -U spotify_user \
  -d spotify_tools --clean -v /tmp/restore.dump

# Or drop database first (see Scenario 1 above)
```

---

## ⚙️ Configuration

### Change Backup Frequency

```bash
# Edit cron schedule
crontab -e

# Examples:
# Every hour:        0 * * * * /Users/bretthardman/backup-spotify-db.sh
# Every 6 hours:     0 */6 * * * /Users/bretthardman/backup-spotify-db.sh
# Daily at 2 AM:     0 2 * * * /Users/bretthardman/backup-spotify-db.sh (current)
# Weekly Sunday 3AM: 0 3 * * 0 /Users/bretthardman/backup-spotify-db.sh
```

### Change Retention Period

```bash
# Edit script
nano ~/backup-spotify-db.sh

# Find this line:
find $BACKUP_DIR -name "backup-*.dump" -mtime +30 -delete

# Change +30 to different number of days:
# Keep 7 days:  -mtime +7
# Keep 60 days: -mtime +60
# Keep 90 days: -mtime +90
```

### Change Backup Location

```bash
# Edit script
nano ~/backup-spotify-db.sh

# Change this line:
BACKUP_DIR=~/spotify-backups

# To new location (example):
BACKUP_DIR=/Volumes/ExternalDrive/spotify-backups
# or
BACKUP_DIR=~/Dropbox/spotify-backups
```

---

## 📋 Quick Reference

### Important Files
| Path | Purpose |
|------|---------|
| `~/backup-spotify-db.sh` | Backup script |
| `~/spotify-backups/` | Backup storage |
| `~/spotify-backups/backup.log` | Backup activity log |
| `~/spotify_tools/docker-compose.yml` | Database configuration |

### Important Commands
| Task | Command |
|------|---------|
| Manual backup | `~/backup-spotify-db.sh` |
| View backups | `ls -lh ~/spotify-backups/` |
| Check log | `tail ~/spotify-backups/backup.log` |
| View cron jobs | `crontab -l` |
| Test restore | See "Scenario 3" above |

### Database Info
| Setting | Value |
|---------|-------|
| Container | `spotify-tools-db` |
| Database | `spotify_tools` |
| Username | `spotify_user` |
| Port | 5433 (external) |
| Size | ~18 MB (uncompressed) |
| Backup size | ~1.5 MB (compressed) |

---

## 🔐 Security Notes

- Backups contain **sensitive data** (Spotify tokens, listening history)
- Stored in **your home directory** (only you can access)
- **Not encrypted** - consider encrypting if storing on cloud/external drive
- **Passwords** stored in `.env` file (not in backups, but needed to restore)

### Encrypting a Backup (Optional)
```bash
# Encrypt backup with password
gpg -c ~/spotify-backups/backup-20260105-020000.dump
# Creates: backup-20260105-020000.dump.gpg

# Decrypt when needed
gpg ~/spotify-backups/backup-20260105-020000.dump.gpg
```

---

## ✅ Backup Health Checklist

Run this monthly to verify backups are working:

```bash
# 1. Check backup directory exists and has recent files
ls -lht ~/spotify-backups/ | head -10
# Should see files from last 30 days

# 2. Check log shows successful backups
tail -50 ~/spotify-backups/backup.log | grep "✅"
# Should see daily successes

# 3. Check disk space (backups shouldn't exceed ~50 MB)
du -sh ~/spotify-backups/
# Should be under 50 MB

# 4. Verify one backup is valid
LATEST=$(ls -t ~/spotify-backups/backup-*.dump | head -1)
docker cp $LATEST spotify-tools-db:/tmp/verify.dump
docker exec spotify-tools-db pg_restore --list /tmp/verify.dump | head -5
docker exec spotify-tools-db rm /tmp/verify.dump
# Should list database objects

# 5. Check cron is still scheduled
crontab -l | grep spotify
# Should show: 0 2 * * * /Users/bretthardman/backup-spotify-db.sh
```

---

## 📞 Getting Help

If something goes wrong:

1. **Check the log first:** `cat ~/spotify-backups/backup.log | tail -50`
2. **Test manually:** `~/backup-spotify-db.sh`
3. **Check container:** `docker ps | grep spotify-tools-db`
4. **Check disk space:** `df -h ~`

---

**Last Updated:** 2026-01-05  
**Next Review:** 2026-02-05 (monthly health check)
