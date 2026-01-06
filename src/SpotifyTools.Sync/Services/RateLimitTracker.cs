using SpotifyTools.Data;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace SpotifyTools.Sync.Services;

public interface IRateLimitTracker
{
    Task<bool> CanMakeRequestAsync();
    Task RecordRequestAsync();
    Task RecordRateLimitHitAsync(DateTime? retryAfter = null);
    Task<RateLimitState?> GetCurrentStateAsync();
}

public class RateLimitTracker : IRateLimitTracker
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RateLimitTracker> _logger;
    private const string StateKey = "spotify_api";

    public RateLimitTracker(IUnitOfWork unitOfWork, ILogger<RateLimitTracker> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> CanMakeRequestAsync()
    {
        var state = await GetOrCreateStateAsync();
        
        if (state.IsRateLimited && state.RetryAfter.HasValue)
        {
            if (DateTime.UtcNow < state.RetryAfter.Value)
            {
                return false;
            }
            
            // Rate limit period has passed
            state.IsRateLimited = false;
            state.RetryAfter = null;
            await _unitOfWork.SaveChangesAsync();
        }

        return !state.IsRateLimited;
    }

    public async Task RecordRequestAsync()
    {
        var state = await GetOrCreateStateAsync();
        state.RequestCount++;
        state.LastRequestAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RecordRateLimitHitAsync(DateTime? retryAfter = null)
    {
        var state = await GetOrCreateStateAsync();
        state.IsRateLimited = true;
        state.RetryAfter = retryAfter ?? DateTime.UtcNow.AddHours(24);
        state.LastRateLimitAt = DateTime.UtcNow;
        
        _logger.LogWarning("Rate limit hit. Retry after: {RetryAfter}", state.RetryAfter);
        
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<RateLimitState?> GetCurrentStateAsync()
    {
        return await GetOrCreateStateAsync();
    }

    private async Task<RateLimitState> GetOrCreateStateAsync()
    {
        var repo = _unitOfWork.RateLimitStates;
        var state = (await repo.GetAllAsync()).FirstOrDefault(s => s.Key == StateKey);

        if (state == null)
        {
            state = new RateLimitState
            {
                Key = StateKey,
                IsRateLimited = false,
                RequestCount = 0,
                LastRequestAt = DateTime.UtcNow,
                WindowStart = DateTime.UtcNow
            };
            await repo.AddAsync(state);
            await _unitOfWork.SaveChangesAsync();
        }

        return state;
    }
}
