using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SpotifyTools.PlaybackWorker.Services;

/// <summary>
/// Service for sending email alerts about critical errors
/// </summary>
public interface IEmailAlertService
{
    Task SendAuthenticationFailureAlertAsync(Exception exception);
    Task SendConsecutiveErrorsAlertAsync(int errorCount, Exception lastException);
}

public class EmailAlertService : IEmailAlertService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailAlertService> _logger;
    private readonly bool _enabled;
    private readonly string? _smtpHost;
    private readonly int _smtpPort;
    private readonly bool _useSsl;
    private readonly string? _senderEmail;
    private readonly string? _senderPassword;
    private readonly string? _recipientEmail;
    private readonly bool _alertOnAuthFailure;
    private readonly bool _alertOnConsecutiveErrors;

    public EmailAlertService(IConfiguration configuration, ILogger<EmailAlertService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Load configuration
        _enabled = configuration.GetValue<bool>("EmailAlerts:Enabled");
        _smtpHost = configuration["EmailAlerts:SmtpHost"];
        _smtpPort = configuration.GetValue<int>("EmailAlerts:SmtpPort", 587);
        _useSsl = configuration.GetValue<bool>("EmailAlerts:UseSsl", true);
        _senderEmail = configuration["EmailAlerts:SenderEmail"];
        _senderPassword = configuration["EmailAlerts:SenderPassword"];
        _recipientEmail = configuration["EmailAlerts:RecipientEmail"];
        _alertOnAuthFailure = configuration.GetValue<bool>("EmailAlerts:AlertOnAuthFailure", true);
        _alertOnConsecutiveErrors = configuration.GetValue<bool>("EmailAlerts:AlertOnConsecutiveErrors", true);

        // Validate configuration if enabled
        if (_enabled)
        {
            if (string.IsNullOrEmpty(_smtpHost) || string.IsNullOrEmpty(_senderEmail) ||
                string.IsNullOrEmpty(_senderPassword) || string.IsNullOrEmpty(_recipientEmail))
            {
                _logger.LogWarning("Email alerts enabled but configuration incomplete. Disabling email alerts.");
                _enabled = false;
            }
        }
    }

    public async Task SendAuthenticationFailureAlertAsync(Exception exception)
    {
        if (!_enabled || !_alertOnAuthFailure)
            return;

        var subject = "🚨 Spotify PlaybackWorker: Authentication Failed";
        var body = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2 style='color: #d32f2f;'>🚨 Authentication Failure</h2>
    <p>Your Spotify PlaybackWorker service failed to authenticate with Spotify.</p>
    
    <h3>What This Means:</h3>
    <ul>
        <li>The stored refresh token may have expired</li>
        <li>Playback tracking and syncing are paused</li>
        <li>You need to re-authenticate once to resume service</li>
    </ul>
    
    <h3>How to Fix:</h3>
    <ol>
        <li>Open a terminal on your Mac</li>
        <li>Run: <code>cd /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.Web && dotnet run</code></li>
        <li>Navigate to <a href='http://localhost:5241'>http://localhost:5241</a></li>
        <li>The app will authenticate and save a new token</li>
        <li>The daemon will resume automatically</li>
    </ol>
    
    <h3>Error Details:</h3>
    <pre style='background: #f5f5f5; padding: 10px; border-radius: 5px;'>{exception.Message}

{exception.StackTrace}</pre>
    
    <p style='color: #666; font-size: 12px;'>
        Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}<br/>
        Host: {Environment.MachineName}
    </p>
</body>
</html>
";

        await SendEmailAsync(subject, body);
    }

    public async Task SendConsecutiveErrorsAlertAsync(int errorCount, Exception lastException)
    {
        if (!_enabled || !_alertOnConsecutiveErrors)
            return;

        var subject = $"⚠️ Spotify PlaybackWorker: {errorCount} Consecutive Errors";
        var body = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2 style='color: #f57c00;'>⚠️ Consecutive Errors Detected</h2>
    <p>Your Spotify PlaybackWorker service has encountered <strong>{errorCount} consecutive errors</strong>.</p>
    
    <h3>What This Means:</h3>
    <ul>
        <li>The service is experiencing repeated failures</li>
        <li>Possible causes: Network issues, API rate limits, database connectivity, or authentication problems</li>
        <li>The service will continue retrying automatically</li>
    </ul>
    
    <h3>Recommended Actions:</h3>
    <ol>
        <li>Check the logs: <code>tail -100 /Users/bretthardman/_dev/spotify_tools/src/SpotifyTools.PlaybackWorker/logs/playback-worker-*.log</code></li>
        <li>Verify PostgreSQL is running: <code>pg_isready -h localhost -p 5433</code></li>
        <li>Check network connectivity</li>
        <li>Review Spotify API status: <a href='https://developer.spotify.com/status'>https://developer.spotify.com/status</a></li>
    </ol>
    
    <h3>Last Error Details:</h3>
    <pre style='background: #f5f5f5; padding: 10px; border-radius: 5px;'>{lastException.Message}

{lastException.StackTrace}</pre>
    
    <p style='color: #666; font-size: 12px;'>
        Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}<br/>
        Host: {Environment.MachineName}<br/>
        Error Count: {errorCount}
    </p>
</body>
</html>
";

        await SendEmailAsync(subject, body);
    }

    private async Task SendEmailAsync(string subject, string htmlBody)
    {
        if (!_enabled)
            return;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Spotify PlaybackWorker", _senderEmail));
            message.To.Add(new MailboxAddress("", _recipientEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect to SMTP server
            await client.ConnectAsync(_smtpHost, _smtpPort, _useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            
            // Authenticate
            await client.AuthenticateAsync(_senderEmail, _senderPassword);
            
            // Send email
            await client.SendAsync(message);
            
            // Disconnect
            await client.DisconnectAsync(true);

            _logger.LogInformation("Alert email sent successfully to {Recipient}", _recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert email. Check email configuration.");
        }
    }
}
