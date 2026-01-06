namespace SpotifyTools.Domain.Entities;

public class RateLimitState
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public int RequestCount { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime? RateLimitHitAt { get; set; }
    public DateTime? RateLimitResetsAt { get; set; }
    public bool IsRateLimited { get; set; }
    public DateTime? RetryAfter { get; set; }
    public DateTime? LastRequestAt { get; set; }
    public DateTime? LastRateLimitAt { get; set; }
}
