using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Implementations;

/// <summary>
/// Track repository with specialized query methods
/// </summary>
public class TrackRepository : Repository<Track>, ITrackRepository
{
    public TrackRepository(DbContext.SpotifyDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Track>> GetTracksWithAudioFeaturesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.AudioFeatures)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Track>> GetTracksByGenreAsync(string genre, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .Where(t => t.TrackArtists.Any(ta => ta.Artist.Genres.Contains(genre)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Track>> GetTracksAddedAfterAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.AddedAt != null && t.AddedAt > date)
            .OrderByDescending(t => t.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Track?> GetTrackWithDetailsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.AudioFeatures)
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackAlbums)
                .ThenInclude(ta => ta.Album)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Track>> GetTracksByTempoRangeAsync(float minTempo, float maxTempo, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.AudioFeatures)
            .Where(t => t.AudioFeatures != null
                && t.AudioFeatures.Tempo >= minTempo
                && t.AudioFeatures.Tempo <= maxTempo)
            .OrderBy(t => t.AudioFeatures!.Tempo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Track>> GetTracksByKeyAsync(int key, int? mode = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(t => t.AudioFeatures)
            .Where(t => t.AudioFeatures != null && t.AudioFeatures.Key == key);

        if (mode.HasValue)
        {
            query = query.Where(t => t.AudioFeatures!.Mode == mode.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
