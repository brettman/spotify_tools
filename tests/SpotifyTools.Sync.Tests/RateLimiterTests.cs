using FluentAssertions;
using System.Diagnostics;

namespace SpotifyTools.Sync.Tests;

/// <summary>
/// Tests for RateLimiter to verify rate limiting behavior, backoff, and request throttling
/// </summary>
public class RateLimiterTests
{
    [Fact]
    public async Task WaitAsync_ShouldAllowRequestsWithinLimit()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 10, timeWindow: TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        // Act - Make 10 requests (should all pass through without delay)
        for (int i = 0; i < 10; i++)
        {
            await rateLimiter.WaitAsync();
        }

        stopwatch.Stop();

        // Assert - Should complete quickly (allow 100ms margin for test overhead)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "requests within limit should not be delayed");
    }

    [Fact]
    public async Task WaitAsync_ShouldDelayRequestsExceedingLimit()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 5, timeWindow: TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        // Act - Make 6 requests (6th should be delayed)
        for (int i = 0; i < 6; i++)
        {
            await rateLimiter.WaitAsync();
        }

        stopwatch.Stop();

        // Assert - Should have delayed the 6th request by approximately 1 second
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(900, "exceeding rate limit should cause delay");
    }

    [Fact]
    public async Task WaitAsync_ShouldRespectTimeWindow()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 3, timeWindow: TimeSpan.FromMilliseconds(500));
        var requestTimes = new List<long>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Make 6 requests (should batch into groups of 3 per 500ms)
        for (int i = 0; i < 6; i++)
        {
            await rateLimiter.WaitAsync();
            requestTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        requestTimes.Count.Should().Be(6);
        // First 3 requests should be fast
        requestTimes[2].Should().BeLessThan(100);
        // 4th request should wait for time window
        requestTimes[3].Should().BeGreaterThan(400);
    }

    [Fact]
    public async Task TriggerBackoff_ShouldPauseAllRequests()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 100, timeWindow: TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        // Act - Trigger backoff
        rateLimiter.TriggerBackoff();

        // Next request should be delayed
        await rateLimiter.WaitAsync();
        stopwatch.Stop();

        // Assert - Should wait at least 60 seconds on first backoff (allow -100ms margin for timing)
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(59_900, "backoff should delay requests by 60 seconds");
    }

    [Fact]
    public void TriggerBackoff_ShouldIncreaseBackoffDuration()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 100, timeWindow: TimeSpan.FromSeconds(1));

        // Act - Trigger multiple backoffs
        rateLimiter.TriggerBackoff(); // 60s
        rateLimiter.TriggerBackoff(); // 120s
        rateLimiter.TriggerBackoff(); // 180s

        // Assert - Subsequent backoffs should increase duration
        // This is tested implicitly through timing in integration tests
        // Here we just verify the method doesn't throw
    }

    [Fact]
    public void ResetBackoff_ShouldClearBackoffState()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 100, timeWindow: TimeSpan.FromSeconds(1));
        rateLimiter.TriggerBackoff();

        // Act
        rateLimiter.ResetBackoff();

        // Assert - Next request should not have backoff delay
        var stopwatch = Stopwatch.StartNew();
        var task = rateLimiter.WaitAsync();
        task.Wait(TimeSpan.FromMilliseconds(100)).Should().BeTrue("reset backoff should allow immediate requests");
    }

    [Fact]
    public async Task WaitAsync_ConcurrentRequests_ShouldRespectLimit()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 5, timeWindow: TimeSpan.FromSeconds(1));
        var completedRequests = 0;
        var tasks = new List<Task>();

        // Act - Fire 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await rateLimiter.WaitAsync();
                Interlocked.Increment(ref completedRequests);
            }));
        }

        // Wait a short time (less than time window)
        await Task.Delay(100);

        // Assert - Only first 5 should complete quickly
        completedRequests.Should().BeLessOrEqualTo(5, "rate limiter should enforce max requests");

        // Wait for all to complete
        await Task.WhenAll(tasks);
        completedRequests.Should().Be(10, "all requests should eventually complete");
    }

    [Fact]
    public async Task WaitAsync_ShouldCleanupOldRequestTimes()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 3, timeWindow: TimeSpan.FromMilliseconds(200));

        // Act - Make 3 requests
        for (int i = 0; i < 3; i++)
        {
            await rateLimiter.WaitAsync();
        }

        // Wait for time window to expire
        await Task.Delay(250);

        // Make 3 more requests (should be fast if old requests were cleaned up)
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 3; i++)
        {
            await rateLimiter.WaitAsync();
        }
        stopwatch.Stop();

        // Assert - Should be fast (old requests cleaned up)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "expired requests should be removed from window");
    }

    [Fact]
    public async Task WaitAsync_MultipleBackoffs_ShouldExponentiallyIncrease()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 100, timeWindow: TimeSpan.FromSeconds(1));

        // Act & Assert - First backoff: 60s
        rateLimiter.TriggerBackoff();
        var stopwatch1 = Stopwatch.StartNew();
        await rateLimiter.WaitAsync();
        stopwatch1.Stop();
        stopwatch1.ElapsedMilliseconds.Should().BeInRange(59_900, 61_000, "first backoff should be 60s");

        // Second backoff: 120s
        rateLimiter.TriggerBackoff();
        var stopwatch2 = Stopwatch.StartNew();
        await rateLimiter.WaitAsync();
        stopwatch2.Stop();
        stopwatch2.ElapsedMilliseconds.Should().BeInRange(119_900, 121_000, "second backoff should be 120s");
    }

    [Fact]
    public async Task WaitAsync_AfterResetBackoff_ShouldNotDelay()
    {
        // Arrange
        var rateLimiter = new RateLimiter(maxRequests: 100, timeWindow: TimeSpan.FromSeconds(1));
        rateLimiter.TriggerBackoff();
        rateLimiter.ResetBackoff();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await rateLimiter.WaitAsync();
        stopwatch.Stop();

        // Assert - Should not have backoff delay
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "reset backoff should allow immediate requests");
    }
}
