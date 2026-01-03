using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Implementations;

public class SavedClusterRepository : Repository<SavedCluster>, ISavedClusterRepository
{
    public SavedClusterRepository(DbContext.SpotifyDbContext context) : base(context)
    {
    }

    public async Task<SavedCluster?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<SavedCluster>> GetAllOrderedAsync()
    {
        return await _dbSet
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SavedCluster>> GetFinalizedClustersAsync()
    {
        return await _dbSet
            .Where(c => c.IsFinalized)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _dbSet
            .AnyAsync(c => c.Name.ToLower() == name.ToLower());
    }
}
