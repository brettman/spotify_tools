namespace SpotifyTools.Sync;

/// <summary>
/// Rate limiter to control API request frequency
/// </summary>
public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;

    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
    }

    public async Task WaitAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;

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
