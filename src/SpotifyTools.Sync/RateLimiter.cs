namespace SpotifyTools.Sync;

/// <summary>
/// Rate limiter to control API request frequency with global backoff support
/// </summary>
public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;
    private DateTime _backoffUntil = DateTime.MinValue;
    private int _consecutiveRateLimitHits = 0;

    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
    }

    /// <summary>
    /// Trigger a global backoff when hitting a 429 rate limit error.
    /// This pauses ALL API calls for an exponentially increasing duration.
    /// </summary>
    public void TriggerBackoff()
    {
        lock (_requestTimes)
        {
            _consecutiveRateLimitHits++;

            // Exponential backoff: 60s, 120s, 180s
            var backoffSeconds = Math.Min(60 * _consecutiveRateLimitHits, 180);
            _backoffUntil = DateTime.UtcNow.AddSeconds(backoffSeconds);

            Console.WriteLine($"⏸️  Global rate limit backoff activated: {backoffSeconds}s (hit #{_consecutiveRateLimitHits})");
        }
    }

    /// <summary>
    /// Reset the consecutive rate limit counter when requests succeed
    /// </summary>
    public void ResetBackoff()
    {
        lock (_requestTimes)
        {
            if (_consecutiveRateLimitHits > 0)
            {
                _consecutiveRateLimitHits = 0;
                _backoffUntil = DateTime.MinValue; // Clear the backoff timestamp
                Console.WriteLine("✓ Rate limit backoff reset - requests flowing normally");
            }
        }
    }

    public async Task WaitAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;

            // Check if we're in a global backoff period
            if (_backoffUntil > now)
            {
                var backoffWait = _backoffUntil - now;
                Console.WriteLine($"⏸️  Waiting {backoffWait.TotalSeconds:F0}s due to global rate limit backoff...");
                await Task.Delay(backoffWait);
                now = DateTime.UtcNow;
            }

            // Remove requests outside the time window
            while (_requestTimes.Count > 0 && (now - _requestTimes.Peek()) > _timeWindow)
            {
                _requestTimes.Dequeue();
            }

            // If we've hit the limit, wait until the oldest request expires
            if (_requestTimes.Count >= _maxRequests)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = _timeWindow - (now - oldestRequest);

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime);
                }

                // Remove the oldest request
                _requestTimes.Dequeue();
            }

            // Record this request
            _requestTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
