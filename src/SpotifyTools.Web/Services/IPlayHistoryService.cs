using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Web.Services;

/// <summary>
/// Service for managing play history tracking
/// </summary>
public interface IPlayHistoryService
{
    /// <summary>
    /// Gets the timestamp of the most recent play event in our database
    /// </summary>
    Task<DateTime?> GetLastPlayTimestampAsync();

    /// <summary>
    /// Saves a batch of play history records
    /// </summary>
    Task SavePlayHistoryBatchAsync(List<PlayHistory> playHistories);

    /// <summary>
    /// Gets total play count for a specific track
    /// </summary>
    Task<int> GetTrackPlayCountAsync(string trackId, DateTime? since = null);

    /// <summary>
    /// Gets play history for a specific track
    /// </summary>
    Task<List<PlayHistory>> GetTrackPlayHistoryAsync(string trackId, int limit = 50);

    /// <summary>
    /// Gets total plays across all tracks
    /// </summary>
    Task<int> GetTotalPlaysAsync(DateTime? since = null);
}
