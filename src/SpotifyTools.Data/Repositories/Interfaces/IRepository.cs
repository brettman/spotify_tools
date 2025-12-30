namespace SpotifyTools.Data.Repositories.Interfaces;

/// <summary>
/// Generic repository interface for basic CRUD operations
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Get entity by ID
    /// </summary>
    Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all entities
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new entity
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple entities
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing entity
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Delete an entity
    /// </summary>
    void Delete(TEntity entity);

    /// <summary>
    /// Delete multiple entities
    /// </summary>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Check if entity exists by ID
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
