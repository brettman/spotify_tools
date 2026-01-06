using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Implementations;

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

    public async Task<SyncState?> GetByKeyAsync(string stateKey, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SyncState>()
            .FirstOrDefaultAsync(s => s.StateKey == stateKey, cancellationToken);
    }

    public async Task<List<SyncState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<SyncState>()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SyncState syncState, CancellationToken cancellationToken = default)
    {
        await _context.Set<SyncState>().AddAsync(syncState, cancellationToken);
    }

    public async Task UpdateAsync(SyncState syncState, CancellationToken cancellationToken = default)
    {
        _context.Set<SyncState>().Update(syncState);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<SyncState>()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        
        if (entity != null)
        {
            _context.Set<SyncState>().Remove(entity);
        }
    }
}
