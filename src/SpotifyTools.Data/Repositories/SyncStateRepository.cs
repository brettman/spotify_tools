using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories;

/// <summary>
/// Repository for managing sync state operations
/// </summary>
public class SyncStateRepository : ISyncStateRepository
{
    private readonly SpotifyDbContext _context;

    public SyncStateRepository(SpotifyDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SyncState> GetOrCreateAsync(string entityType, string phase, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SyncStates
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.Phase == phase, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var newState = new SyncState
        {
            EntityType = entityType,
            Phase = phase,
            LastSyncedOffset = 0,
            TotalEstimated = 0,
            IsComplete = false,
            StartedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _context.SyncStates.AddAsync(newState, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return newState;
    }

    public async Task<SyncState?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SyncStates
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<SyncState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SyncStates
            .OrderBy(s => s.EntityType)
            .ThenBy(s => s.Phase)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SyncState>> GetByPhaseAsync(string phase, CancellationToken cancellationToken = default)
    {
        return await _context.SyncStates
            .Where(s => s.Phase == phase)
            .OrderBy(s => s.EntityType)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateCheckpointAsync(int id, int offset, int? totalEstimated = null, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.LastSyncedOffset = offset;
        if (totalEstimated.HasValue)
        {
            state.TotalEstimated = totalEstimated.Value;
        }
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRateLimitedAsync(int id, DateTime resetAt, int? remaining = null, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.RateLimitHitAt = DateTime.UtcNow;
        state.RateLimitResetAt = resetAt;
        state.RateLimitRemaining = remaining;
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearRateLimitAsync(int id, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.RateLimitHitAt = null;
        state.RateLimitResetAt = null;
        state.RateLimitRemaining = null;
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.IsComplete = true;
        state.CompletedAt = DateTime.UtcNow;
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordErrorAsync(int id, string errorMessage, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.ErrorMessage = errorMessage?.Length > 2000 
            ? errorMessage.Substring(0, 2000) 
            : errorMessage;
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsRateLimitedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.SyncStates
            .AnyAsync(s => s.RateLimitResetAt.HasValue && s.RateLimitResetAt.Value > now, cancellationToken);
    }

    public async Task<DateTime?> GetEarliestRateLimitResetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.SyncStates
            .Where(s => s.RateLimitResetAt.HasValue && s.RateLimitResetAt.Value > now)
            .OrderBy(s => s.RateLimitResetAt)
            .Select(s => s.RateLimitResetAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ResetAsync(int id, CancellationToken cancellationToken = default)
    {
        var state = await GetByIdAsync(id, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"SyncState with id {id} not found");
        }

        state.LastSyncedOffset = 0;
        state.IsComplete = false;
        state.CompletedAt = null;
        state.RateLimitHitAt = null;
        state.RateLimitResetAt = null;
        state.RateLimitRemaining = null;
        state.ErrorMessage = null;
        state.StartedAt = DateTime.UtcNow;
        state.LastUpdatedAt = DateTime.UtcNow;

        _context.SyncStates.Update(state);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
