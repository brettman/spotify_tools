using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for managing play history tracking
/// </summary>
public class PlayHistoryService : IPlayHistoryService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlayHistoryService> _logger;

    public PlayHistoryService(
        SpotifyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<PlayHistoryService> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DateTime?> GetLastPlayTimestampAsync()
    {
        try
        {
            return await _dbContext.PlayHistories
                .OrderByDescending(ph => ph.PlayedAt)
                .Select(ph => (DateTime?)ph.PlayedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last play timestamp");
            throw;
        }
    }

    public async Task SavePlayHistoryBatchAsync(List<PlayHistory> playHistories)
    {
        try
        {
            if (!playHistories.Any())
                return;

            // Check for duplicates based on TrackId + PlayedAt combination
            var existingPlays = await _dbContext.PlayHistories
                .Where(ph => playHistories.Select(p => p.TrackId).Contains(ph.TrackId))
                .Select(ph => new { ph.TrackId, ph.PlayedAt })
                .ToListAsync();

            var existingPlaySet = existingPlays
                .Select(ep => $"{ep.TrackId}_{ep.PlayedAt:yyyy-MM-ddTHH:mm:ss}")
                .ToHashSet();

            // Filter out duplicates
            var newPlays = playHistories
                .Where(ph => !existingPlaySet.Contains($"{ph.TrackId}_{ph.PlayedAt:yyyy-MM-ddTHH:mm:ss}"))
                .ToList();

            if (!newPlays.Any())
            {
                _logger.LogInformation("No new play history records to save (all duplicates)");
                return;
            }

            // Add new records
            await _dbContext.PlayHistories.AddRangeAsync(newPlays);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} new play history records", newPlays.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving play history batch");
            throw;
        }
    }

    public async Task<int> GetTrackPlayCountAsync(string trackId, DateTime? since = null)
    {
        try
        {
            var query = _dbContext.PlayHistories
                .Where(ph => ph.TrackId == trackId);

            if (since.HasValue)
            {
                query = query.Where(ph => ph.PlayedAt >= since.Value);
            }

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting play count for track {TrackId}", trackId);
            throw;
        }
    }

    public async Task<List<PlayHistory>> GetTrackPlayHistoryAsync(string trackId, int limit = 50)
    {
        try
        {
            return await _dbContext.PlayHistories
                .Where(ph => ph.TrackId == trackId)
                .OrderByDescending(ph => ph.PlayedAt)
                .Take(limit)
                .Include(ph => ph.Track)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting play history for track {TrackId}", trackId);
            throw;
        }
    }

    public async Task<int> GetTotalPlaysAsync(DateTime? since = null)
    {
        try
        {
            var query = _dbContext.PlayHistories.AsQueryable();

            if (since.HasValue)
            {
                query = query.Where(ph => ph.PlayedAt >= since.Value);
            }

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total plays");
            throw;
        }
    }
}
