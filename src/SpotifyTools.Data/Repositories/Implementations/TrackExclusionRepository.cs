using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Implementations;

public class TrackExclusionRepository : Repository<TrackExclusion>, ITrackExclusionRepository
{
    public TrackExclusionRepository(DbContext.SpotifyDbContext context) : base(context)
    {
    }

    public async Task<List<TrackExclusion>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.ClusterId == clusterId)
            .OrderByDescending(e => e.ExcludedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsTrackExcludedAsync(int clusterId, string trackId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(e => e.ClusterId == clusterId && e.TrackId == trackId, cancellationToken);
    }

    public async Task AddExclusionAsync(int clusterId, string trackId, CancellationToken cancellationToken = default)
    {
        // Check if exclusion already exists
        var exists = await IsTrackExcludedAsync(clusterId, trackId, cancellationToken);
        if (exists)
        {
            return; // Already excluded, no action needed
        }

        var exclusion = new TrackExclusion
        {
            ClusterId = clusterId,
            TrackId = trackId,
            ExcludedAt = DateTime.UtcNow
        };

        await _dbSet.AddAsync(exclusion, cancellationToken);
    }

    public async Task RemoveExclusionAsync(int clusterId, string trackId, CancellationToken cancellationToken = default)
    {
        var exclusion = await _dbSet
            .FirstOrDefaultAsync(e => e.ClusterId == clusterId && e.TrackId == trackId, cancellationToken);

        if (exclusion != null)
        {
            _dbSet.Remove(exclusion);
        }
    }

    public async Task<HashSet<string>> GetExcludedTrackIdsAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var trackIds = await _dbSet
            .Where(e => e.ClusterId == clusterId)
            .Select(e => e.TrackId)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(trackIds);
    }
}
