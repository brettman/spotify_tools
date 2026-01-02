using Microsoft.EntityFrameworkCore;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.DbContext;

/// <summary>
/// Entity Framework Core database context for Spotify data
/// </summary>
public class SpotifyDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public SpotifyDbContext(DbContextOptions<SpotifyDbContext> options) : base(options)
    {
    }

    // Entity sets
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<AudioFeatures> AudioFeatures => Set<AudioFeatures>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<TrackArtist> TrackArtists => Set<TrackArtist>();
    public DbSet<TrackAlbum> TrackAlbums => Set<TrackAlbum>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<SpotifyToken> SpotifyTokens => Set<SpotifyToken>();
    public DbSet<SyncHistory> SyncHistory => Set<SyncHistory>();
    public DbSet<AudioAnalysis> AudioAnalyses => Set<AudioAnalysis>();
    public DbSet<AudioAnalysisSection> AudioAnalysisSections => Set<AudioAnalysisSection>();
    public DbSet<SavedCluster> SavedClusters => Set<SavedCluster>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Track configuration
        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("tracks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Isrc).HasMaxLength(20);

            // JSONB column for extended metadata (PostgreSQL specific)
            entity.Property(e => e.ExtendedMetadata)
                .HasColumnType("jsonb");

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.AddedAt);
        });

        // Artist configuration
        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("artists");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            // Array type for genres (PostgreSQL specific)
            entity.Property(e => e.Genres)
                .HasColumnType("text[]");

            entity.HasIndex(e => e.Name);
        });

        // Album configuration
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("albums");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.AlbumType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ReleaseDate);
        });

        // AudioFeatures configuration
        modelBuilder.Entity<AudioFeatures>(entity =>
        {
            entity.ToTable("audio_features");
            entity.HasKey(e => e.TrackId);
            entity.Property(e => e.TrackId).HasMaxLength(50);

            // One-to-one relationship with Track
            entity.HasOne(e => e.Track)
                .WithOne(t => t.AudioFeatures)
                .HasForeignKey<AudioFeatures>(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index on key audio features for analytics
            entity.HasIndex(e => e.Tempo);
            entity.HasIndex(e => e.Key);
            entity.HasIndex(e => e.Energy);
            entity.HasIndex(e => e.Danceability);
        });

        // AudioAnalysis configuration
        modelBuilder.Entity<AudioAnalysis>(entity =>
        {
            entity.ToTable("audio_analyses");
            entity.HasKey(e => e.TrackId);
            entity.Property(e => e.TrackId).HasMaxLength(50);

            // One-to-one relationship with Track
            entity.HasOne(e => e.Track)
                .WithOne(t => t.AudioAnalysis)
                .HasForeignKey<AudioAnalysis>(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many relationship with Sections
            entity.HasMany(e => e.Sections)
                .WithOne(s => s.AudioAnalysis)
                .HasForeignKey(s => s.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.FetchedAt);
        });

        // AudioAnalysisSection configuration
        modelBuilder.Entity<AudioAnalysisSection>(entity =>
        {
            entity.ToTable("audio_analysis_sections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrackId).HasMaxLength(50).IsRequired();

            // Indexes for analytics queries
            entity.HasIndex(e => e.TrackId);
            entity.HasIndex(e => e.Key);
            entity.HasIndex(e => e.Tempo);
            entity.HasIndex(e => e.TimeSignature);
        });

        // Playlist configuration
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("playlists");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.OwnerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SnapshotId).HasMaxLength(100).IsRequired();

            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.SnapshotId);
        });

        // TrackArtist configuration (many-to-many)
        modelBuilder.Entity<TrackArtist>(entity =>
        {
            entity.ToTable("track_artists");
            entity.HasKey(e => new { e.TrackId, e.ArtistId });

            entity.Property(e => e.TrackId).HasMaxLength(50);
            entity.Property(e => e.ArtistId).HasMaxLength(50);

            entity.HasOne(e => e.Track)
                .WithMany(t => t.TrackArtists)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.TrackArtists)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Position);
        });

        // TrackAlbum configuration
        modelBuilder.Entity<TrackAlbum>(entity =>
        {
            entity.ToTable("track_albums");
            entity.HasKey(e => new { e.TrackId, e.AlbumId });

            entity.Property(e => e.TrackId).HasMaxLength(50);
            entity.Property(e => e.AlbumId).HasMaxLength(50);

            entity.HasOne(e => e.Track)
                .WithMany(t => t.TrackAlbums)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Album)
                .WithMany(a => a.TrackAlbums)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlaylistTrack configuration
        modelBuilder.Entity<PlaylistTrack>(entity =>
        {
            entity.ToTable("playlist_tracks");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PlaylistId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TrackId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AddedBy).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Playlist)
                .WithMany(p => p.PlaylistTracks)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Track)
                .WithMany(t => t.PlaylistTracks)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PlaylistId, e.Position });
            entity.HasIndex(e => e.AddedAt);
        });

        // SpotifyToken configuration
        modelBuilder.Entity<SpotifyToken>(entity =>
        {
            entity.ToTable("spotify_tokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserIdentifier).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EncryptedRefreshToken).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(255);

            entity.HasIndex(e => e.UserIdentifier).IsUnique();
        });

        // SyncHistory configuration
        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.ToTable("sync_history");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserIdentifier).HasMaxLength(100);

            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SyncType);
        });

        // SavedCluster configuration
        modelBuilder.Entity<SavedCluster>(entity =>
        {
            entity.ToTable("saved_clusters");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Genres).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.PrimaryGenre).HasMaxLength(100);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsFinalized);
        });
    }
}
