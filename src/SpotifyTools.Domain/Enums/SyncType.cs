namespace SpotifyTools.Domain.Enums;

/// <summary>
/// Type of sync operation
/// </summary>
public enum SyncType
{
    /// <summary>
    /// Full import of all data
    /// </summary>
    Full = 0,

    /// <summary>
    /// Incremental sync of only changed data
    /// </summary>
    Incremental = 1
}
