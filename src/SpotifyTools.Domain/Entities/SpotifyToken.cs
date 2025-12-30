namespace SpotifyTools.Domain.Entities;

/// <summary>
/// Stores encrypted Spotify OAuth refresh tokens
/// </summary>
public class SpotifyToken
{
    /// <summary>
    /// Auto-incrementing ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Spotify user ID (unique)
    /// </summary>
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted refresh token
    /// </summary>
    public string EncryptedRefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the token expires (optional - refresh tokens typically don't expire)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When this token was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this token was last used
    /// </summary>
    public DateTime LastUsedAt { get; set; }

    /// <summary>
    /// Display name of the Spotify user (for convenience)
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Email of the Spotify user (if available)
    /// </summary>
    public string? Email { get; set; }
}
