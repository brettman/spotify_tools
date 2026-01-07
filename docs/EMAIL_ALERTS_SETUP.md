# Email Alert System - Setup Guide

**Status:** ✅ Implemented and Running  
**Date:** January 6, 2026

---

## Overview

The PlaybackWorker now sends email alerts for critical issues:
1. **Authentication Failure** - When refresh token expires
2. **Consecutive Errors** - After 5+ consecutive failures

---

## Configuration

### 1. Edit `appsettings.json`

Add your email settings to `/Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/appsettings.json`:

```json
{
  "EmailAlerts": {
    "Enabled": true,
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "SenderEmail": "your.email@gmail.com",
    "SenderPassword": "your-app-password-here",
    "RecipientEmail": "your.email@gmail.com",
    "AlertOnAuthFailure": true,
    "AlertOnConsecutiveErrors": true
  }
}
```

### 2. Gmail App Password (Recommended)

**If using Gmail**, you need an App Password (not your regular password):

1. Go to [Google Account Security](https://myaccount.google.com/security)
2. Enable 2-Factor Authentication (required for App Passwords)
3. Go to [App Passwords](https://myaccount.google.com/apppasswords)
4. Generate new app password:
   - App: Mail
   - Device: Mac
5. Copy the 16-character password
6. Use it as `SenderPassword` in appsettings.json

### 3. Other Email Providers

**Outlook/Hotmail:**
```json
{
  "SmtpHost": "smtp-mail.outlook.com",
  "SmtpPort": 587
}
```

**Yahoo:**
```json
{
  "SmtpHost": "smtp.mail.yahoo.com",
  "SmtpPort": 587
}
```

**iCloud:**
```json
{
  "SmtpHost": "smtp.mail.me.com",
  "SmtpPort": 587
}
```

**Custom SMTP Server:**
```json
{
  "SmtpHost": "mail.yourdomain.com",
  "SmtpPort": 465,  // or 587 for TLS
  "UseSsl": true
}
```

---

## Alert Types

### 1. Authentication Failure Alert

**Triggered when:**
- Stored refresh token expires or is invalid
- Cannot authenticate with Spotify

**Email includes:**
- What happened (token expired)
- Impact (tracking/syncing paused)
- Step-by-step fix instructions
- Error details and stack trace

**Example email:**
```
Subject: 🚨 Spotify PlaybackWorker: Authentication Failed

Your Spotify PlaybackWorker service failed to authenticate with Spotify.

What This Means:
• The stored refresh token may have expired
• Playback tracking and syncing are paused
• You need to re-authenticate once to resume service

How to Fix:
1. Open a terminal on your Mac
2. Run: cd /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.Web && dotnet run
3. Navigate to http://localhost:5241
4. The app will authenticate and save a new token
5. The daemon will resume automatically
```

### 2. Consecutive Errors Alert

**Triggered when:**
- Service encounters 5+ consecutive errors
- Configurable via `MaxConsecutiveErrors` in appsettings.json

**Email includes:**
- Error count
- Possible causes
- Troubleshooting steps
- Last error details

**Example email:**
```
Subject: ⚠️ Spotify PlaybackWorker: 5 Consecutive Errors

Your Spotify PlaybackWorker service has encountered 5 consecutive errors.

What This Means:
• The service is experiencing repeated failures
• Possible causes: Network issues, API rate limits, database connectivity
• The service will continue retrying automatically

Recommended Actions:
1. Check the logs
2. Verify PostgreSQL is running
3. Check network connectivity
4. Review Spotify API status
```

---

## Testing Your Email Setup

### Option 1: Temporarily Lower Threshold
Edit `appsettings.json`:
```json
{
  "PlaybackTracking": {
    "MaxConsecutiveErrors": 1  // Lower to 1 for testing
  }
}
```

Then trigger an error (e.g., stop PostgreSQL briefly), wait for alert email, then restore settings.

### Option 2: Manual Test Command
Run this in terminal to test SMTP connection:
```bash
cd /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker
dotnet run --test-email
```
*(Note: Not implemented yet, but could be added if needed)*

---

## Troubleshooting

### No Emails Received

**Check 1: Configuration**
```bash
# Verify email settings exist
grep -A 10 "EmailAlerts" /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/appsettings.json
```

**Check 2: Service Logs**
```bash
# Look for email sending attempts
grep -i "email" /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/playback-worker-*.log
```

**Check 3: SMTP Errors**
```bash
# Check for SMTP connection errors
grep -i "smtp\|mailkit" /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/stderr.log
```

### Common Issues

**"Failed to send alert email"**
- Check SMTP host and port
- Verify username/password
- Check if 2FA requires app password
- Test network connectivity to SMTP server

**"Authentication failed" (SMTP)**
- Gmail: Must use App Password (not regular password)
- Outlook: May need to enable "Less secure apps"
- Verify email address is correct

**Emails go to Spam**
- Add sender email to contacts
- Mark first email as "Not Spam"
- Consider using dedicated alerting email

---

## Disabling Email Alerts

If you don't want email alerts:

```json
{
  "EmailAlerts": {
    "Enabled": false
  }
}
```

Or disable specific alert types:
```json
{
  "EmailAlerts": {
    "Enabled": true,
    "AlertOnAuthFailure": false,       // Disable auth alerts
    "AlertOnConsecutiveErrors": false  // Disable error alerts
  }
}
```

---

## Advanced Configuration

### Custom Error Threshold
```json
{
  "PlaybackTracking": {
    "MaxConsecutiveErrors": 10  // Raise to 10 for less sensitive alerting
  }
}
```

### Separate Sender and Recipient
```json
{
  "EmailAlerts": {
    "SenderEmail": "noreply@yourdomain.com",  // Send from
    "RecipientEmail": "you@gmail.com"          // Send to
  }
}
```

### Multiple Recipients (Future Enhancement)
*(Not yet implemented, but could be added)*
```json
{
  "EmailAlerts": {
    "RecipientEmails": [
      "admin@example.com",
      "backup@example.com"
    ]
  }
}
```

---

## Security Considerations

### Protect Your Email Password

**appsettings.json contains your email password!**

1. **Never commit appsettings.json to Git**
   - Already in `.gitignore`
   - Use `appsettings.json.template` for version control

2. **File Permissions**
   ```bash
   # Restrict access to appsettings.json
   chmod 600 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/appsettings.json
   ```

3. **Use App Passwords**
   - Never use your main email password
   - Use provider-specific app passwords
   - Revoke if compromised

4. **Alternative: Environment Variables** *(Future Enhancement)*
   ```bash
   export SMTP_PASSWORD="your-app-password"
   ```

---

## Files Modified

1. **New:** `Services/EmailAlertService.cs` - Email sending service
2. **Modified:** `Program.cs` - Registered EmailAlertService
3. **Modified:** `PlaybackTracker.cs` - Integrated email alerts
4. **Modified:** `appsettings.json.template` - Added email configuration

**Package Added:** `MailKit 4.14.1` (cross-platform email library)

---

## ✅ Email Alerts Active!

Your PlaybackWorker will now email you if:
- Authentication fails (you'll know immediately)
- Service has repeated errors (catch issues early)

**Next Steps:**
1. Add your email settings to `appsettings.json`
2. Restart the daemon: `launchctl unload ~/Library/LaunchAgents/com.spotify.playback.worker.plist && launchctl load ~/Library/LaunchAgents/com.spotify.playback.worker.plist`
3. Monitor for test emails (or wait for a real issue!)

**You're now fully protected from silent failures!** 🎉📧
