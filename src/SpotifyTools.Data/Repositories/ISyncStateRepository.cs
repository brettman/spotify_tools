using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories;

/// <summary>
/// Repository interface for managing sync state operations
/// </summary>
public interface ISyncStateRepository
{
    /// <summary>
    /// Gets or creates a sync state for the given entity type and phase
    /// </summary>
    Task<SyncState> GetOrCreateAsync(string entityType, string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a sync state by ID
    /// </summary>
    Task<SyncState?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sync states
    /// </summary>
    Task<List<SyncState>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync states by phase (e.g., all "initial_sync" states)
    /// </summary>
    Task<List<SyncState>> GetByPhaseAsync(string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the checkpoint (last synced offset)
    /// </summary>
    Task UpdateCheckpointAsync(int id, int offset, int? totalEstimated = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the sync state as rate limited
    /// </summary>
    Task MarkRateLimitedAsync(int id, DateTime resetAt, int? remaining = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the rate limit state (called after rate limit expires)
    /// </summary>
    Task ClearRateLimitAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the sync as complete
    /// </summary>
    Task MarkCompleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an error on the sync state
    /// </summary>
    Task RecordErrorAsync(int id, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity type is currently rate limited
    /// </summary>
    Task<bool> IsRateLimitedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the earliest rate limit reset time across all entities
    /// </summary>
    Task<DateTime?> GetEarliestRateLimitResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a sync state to start over (clears progress but keeps record)
    /// </summary>
    Task ResetAsync(int id, CancellationToken cancellationToken = default);
}
