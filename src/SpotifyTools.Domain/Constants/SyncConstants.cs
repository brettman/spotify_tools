namespace SpotifyTools.Domain.Constants;

/// <summary>
/// Constants for sync entity types
/// </summary>
public static class SyncEntityType
{
    public const string Tracks = "tracks";
    public const string Artists = "artists";
    public const string Albums = "albums";
    public const string Playlists = "playlists";
}

/// <summary>
/// Constants for sync phases
/// </summary>
public static class SyncPhase
{
    public const string InitialSync = "initial_sync";
    public const string IncrementalSync = "incremental_sync";
}
