using Microsoft.EntityFrameworkCore;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Web.DTOs;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for listening analytics and statistics
/// </summary>
public class ListeningAnalyticsService : IListeningAnalyticsService
{
    private readonly SpotifyDbContext _dbContext;
    private readonly ILogger<ListeningAnalyticsService> _logger;

    public ListeningAnalyticsService(
        SpotifyDbContext dbContext,
        ILogger<ListeningAnalyticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ListeningStatsDto> GetOverallStatsAsync(TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = _dbContext.PlayHistories.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            var totalPlays = await query.CountAsync();
            var uniqueTracks = await query.Select(ph => ph.TrackId).Distinct().CountAsync();
            var totalTime = await query.SumAsync(ph => (long?)ph.Track.DurationMs) ?? 0;
            var firstPlay = await query.MinAsync(ph => (DateTime?)ph.PlayedAt);
            var lastPlay = await query.MaxAsync(ph => (DateTime?)ph.PlayedAt);

            // Count unique artists
            var uniqueArtists = await _dbContext.PlayHistories
                .Where(ph => startDate == null || ph.PlayedAt >= startDate.Value)
                .Where(ph => endDate == null || ph.PlayedAt <= endDate.Value)
                .SelectMany(ph => ph.Track.TrackArtists.Select(ta => ta.ArtistId))
                .Distinct()
                .CountAsync();

            var daysTracked = firstPlay.HasValue && lastPlay.HasValue
                ? (lastPlay.Value - firstPlay.Value).Days + 1
                : 0;

            var avgPlaysPerDay = daysTracked > 0 ? (double)totalPlays / daysTracked : 0;

            return new ListeningStatsDto
            {
                TotalPlays = totalPlays,
                UniqueTracksPlayed = uniqueTracks,
                UniqueArtistsPlayed = uniqueArtists,
                TotalListeningTimeMs = totalTime,
                FirstPlay = firstPlay,
                LastPlay = lastPlay,
                DaysTracked = daysTracked,
                AveragePlaysPerDay = avgPlaysPerDay
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overall stats");
            throw;
        }
    }

    public async Task<List<TrackPlayCountDto>> GetMostPlayedTracksAsync(int top = 50, TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = from t in _dbContext.Tracks
                        join ph in _dbContext.PlayHistories on t.Id equals ph.TrackId
                        where (startDate == null || ph.PlayedAt >= startDate.Value) &&
                              (endDate == null || ph.PlayedAt <= endDate.Value)
                        group ph by new { t.Id, t.Name, t.DurationMs, t.Popularity } into g
                        orderby g.Count() descending
                        select new
                        {
                            g.Key.Id,
                            g.Key.Name,
                            g.Key.DurationMs,
                            g.Key.Popularity,
                            PlayCount = g.Count(),
                            LastPlayed = g.Max(ph => ph.PlayedAt)
                        };

            var tracks = await query.Take(top).ToListAsync();

            // Get artists for each track
            var result = new List<TrackPlayCountDto>();
            foreach (var track in tracks)
            {
                var artists = await _dbContext.TrackArtists
                    .Where(ta => ta.TrackId == track.Id)
                    .OrderBy(ta => ta.Position)
                    .Select(ta => ta.Artist.Name)
                    .ToListAsync();

                result.Add(new TrackPlayCountDto
                {
                    Id = track.Id,
                    Name = track.Name,
                    Artists = string.Join(", ", artists),
                    PlayCount = track.PlayCount,
                    LastPlayed = track.LastPlayed,
                    DurationMs = track.DurationMs,
                    Popularity = track.Popularity
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most played tracks");
            throw;
        }
    }

    public async Task<List<ArtistPlayCountDto>> GetMostPlayedArtistsAsync(int top = 50, TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var result = await (
                from a in _dbContext.Artists
                join ta in _dbContext.TrackArtists on a.Id equals ta.ArtistId
                join t in _dbContext.Tracks on ta.TrackId equals t.Id
                join ph in _dbContext.PlayHistories on t.Id equals ph.TrackId
                where (startDate == null || ph.PlayedAt >= startDate.Value) &&
                      (endDate == null || ph.PlayedAt <= endDate.Value)
                group new { ph, t } by new { a.Id, a.Name } into g
                orderby g.Count() descending
                select new ArtistPlayCountDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Name,
                    PlayCount = g.Count(),
                    UniqueTrackCount = g.Select(x => x.t.Id).Distinct().Count(),
                    LastPlayed = g.Max(x => x.ph.PlayedAt)
                }
            ).Take(top).ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most played artists");
            throw;
        }
    }

    public async Task<List<GenrePlayCountDto>> GetMostPlayedGenresAsync(int top = 50, TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            // This is complex because genres are stored as arrays on artists
            // We need to use raw SQL for the UNNEST operation
            var sql = @"
                SELECT
                    UNNEST(a.genres) as Genre,
                    COUNT(*) as PlayCount,
                    COUNT(DISTINCT t.id) as UniqueTrackCount,
                    COUNT(DISTINCT a.id) as UniqueArtistCount
                FROM play_history ph
                INNER JOIN tracks t ON ph.track_id = t.id
                INNER JOIN track_artists ta ON t.id = ta.track_id
                INNER JOIN artists a ON ta.artist_id = a.id
                WHERE a.genres IS NOT NULL AND array_length(a.genres, 1) > 0";

            if (startDate.HasValue)
                sql += $" AND ph.played_at >= '{startDate.Value:yyyy-MM-dd HH:mm:ss}'";
            if (endDate.HasValue)
                sql += $" AND ph.played_at <= '{endDate.Value:yyyy-MM-dd HH:mm:ss}'";

            sql += @"
                GROUP BY UNNEST(a.genres)
                ORDER BY PlayCount DESC
                LIMIT {0}";

            var result = await _dbContext.Database
                .SqlQueryRaw<GenrePlayCountDto>(sql, top)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most played genres");
            throw;
        }
    }

    public async Task<List<PlaysByDateDto>> GetPlaysByDateAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _dbContext.PlayHistories.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            var result = await query
                .GroupBy(ph => ph.PlayedAt.Date)
                .Select(g => new PlaysByDateDto
                {
                    Date = g.Key,
                    PlayCount = g.Count(),
                    UniqueTrackCount = g.Select(ph => ph.TrackId).Distinct().Count()
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plays by date");
            throw;
        }
    }

    public async Task<List<PlaysByHourDto>> GetPlaysByHourAsync(TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = _dbContext.PlayHistories.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            var result = await query
                .GroupBy(ph => ph.PlayedAt.Hour)
                .Select(g => new PlaysByHourDto
                {
                    Hour = g.Key,
                    PlayCount = g.Count(),
                    UniqueTrackCount = g.Select(ph => ph.TrackId).Distinct().Count()
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plays by hour");
            throw;
        }
    }

    public async Task<List<PlaysByDayOfWeekDto>> GetPlaysByDayOfWeekAsync(TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = _dbContext.PlayHistories.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            var result = await query
                .GroupBy(ph => ph.PlayedAt.DayOfWeek)
                .Select(g => new PlaysByDayOfWeekDto
                {
                    DayOfWeek = (int)g.Key,
                    DayName = g.Key.ToString(),
                    PlayCount = g.Count(),
                    UniqueTrackCount = g.Select(ph => ph.TrackId).Distinct().Count()
                })
                .OrderBy(x => x.DayOfWeek)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plays by day of week");
            throw;
        }
    }

    public async Task<List<PlaysByContextDto>> GetPlaysByContextAsync(TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = _dbContext.PlayHistories.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            var result = await query
                .GroupBy(ph => ph.ContextType ?? "unknown")
                .Select(g => new PlaysByContextDto
                {
                    ContextType = g.Key,
                    PlayCount = g.Count(),
                    UniqueTrackCount = g.Select(ph => ph.TrackId).Distinct().Count()
                })
                .OrderByDescending(x => x.PlayCount)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plays by context");
            throw;
        }
    }

    public async Task<List<RecentPlayDto>> GetRecentPlaysAsync(int count = 50)
    {
        try
        {
            var plays = await _dbContext.PlayHistories
                .OrderByDescending(ph => ph.PlayedAt)
                .Take(count)
                .Select(ph => new
                {
                    ph.Id,
                    ph.PlayedAt,
                    TrackName = ph.Track.Name,
                    ph.ContextType,
                    ph.Track.DurationMs,
                    TrackId = ph.Track.Id
                })
                .ToListAsync();

            var result = new List<RecentPlayDto>();
            foreach (var play in plays)
            {
                var artists = await _dbContext.TrackArtists
                    .Where(ta => ta.TrackId == play.TrackId)
                    .OrderBy(ta => ta.Position)
                    .Select(ta => ta.Artist.Name)
                    .ToListAsync();

                result.Add(new RecentPlayDto
                {
                    Id = play.Id,
                    PlayedAt = play.PlayedAt,
                    TrackName = play.TrackName,
                    Artists = string.Join(", ", artists),
                    ContextType = play.ContextType,
                    DurationMs = play.DurationMs
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent plays");
            throw;
        }
    }

    public async Task<int> GetTrackPlayCountAsync(string trackId, TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = _dbContext.PlayHistories.Where(ph => ph.TrackId == trackId);
            if (startDate.HasValue)
                query = query.Where(ph => ph.PlayedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ph => ph.PlayedAt <= endDate.Value);

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track play count for {TrackId}", trackId);
            throw;
        }
    }

    public async Task<int> GetArtistPlayCountAsync(string artistId, TimeRange range = TimeRange.AllTime)
    {
        try
        {
            var (startDate, endDate) = GetDateRange(range);

            var query = from ph in _dbContext.PlayHistories
                        join ta in _dbContext.TrackArtists on ph.TrackId equals ta.TrackId
                        where ta.ArtistId == artistId &&
                              (startDate == null || ph.PlayedAt >= startDate.Value) &&
                              (endDate == null || ph.PlayedAt <= endDate.Value)
                        select ph;

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting artist play count for {ArtistId}", artistId);
            throw;
        }
    }

    private (DateTime? startDate, DateTime? endDate) GetDateRange(TimeRange range)
    {
        var now = DateTime.UtcNow;
        return range switch
        {
            TimeRange.Last7Days => (now.AddDays(-7), now),
            TimeRange.Last30Days => (now.AddDays(-30), now),
            TimeRange.Last90Days => (now.AddDays(-90), now),
            TimeRange.Last365Days => (now.AddDays(-365), now),
            TimeRange.ThisWeek => (now.AddDays(-(int)now.DayOfWeek), now),
            TimeRange.ThisMonth => (new DateTime(now.Year, now.Month, 1), now),
            TimeRange.ThisYear => (new DateTime(now.Year, 1, 1), now),
            _ => (null, null) // AllTime
        };
    }
}
