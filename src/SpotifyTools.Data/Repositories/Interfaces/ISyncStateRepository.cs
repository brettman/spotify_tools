using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing sync state operations
/// </summary>
public interface ISyncStateRepository
{
    /// <summary>
    /// Gets a sync state by state key
    /// </summary>
    Task<SyncState?> GetByKeyAsync(string stateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sync states
    /// </summary>
    Task<List<SyncState>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new sync state
    /// </summary>
    Task AddAsync(SyncState syncState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing sync state
    /// </summary>
    Task UpdateAsync(SyncState syncState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a sync state
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
