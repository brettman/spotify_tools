using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.Repositories.Interfaces;

namespace SpotifyTools.Data.Repositories.Implementations;

/// <summary>
/// Generic repository implementation
/// </summary>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly DbContext.SpotifyDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(DbContext.SpotifyDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Update(TEntity entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Delete(TEntity entity)
    {
        _dbSet.Remove(entity);
    }

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public virtual async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        return entity != null;
    }
}
