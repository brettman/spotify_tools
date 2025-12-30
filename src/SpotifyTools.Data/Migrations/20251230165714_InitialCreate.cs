using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AlbumType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalTracks = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Popularity = table.Column<int>(type: "integer", nullable: false),
                    Followers = table.Column<int>(type: "integer", nullable: false),
                    Genres = table.Column<string[]>(type: "text[]", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spotify_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SyncType = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TracksAdded = table.Column<int>(type: "integer", nullable: false),
                    TracksUpdated = table.Column<int>(type: "integer", nullable: false),
                    ArtistsAdded = table.Column<int>(type: "integer", nullable: false),
                    AlbumsAdded = table.Column<int>(type: "integer", nullable: false),
                    PlaylistsSynced = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    UserIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Explicit = table.Column<bool>(type: "boolean", nullable: false),
                    Popularity = table.Column<int>(type: "integer", nullable: false),
                    Isrc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtendedMetadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audio_features",
                columns: table => new
                {
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Acousticness = table.Column<float>(type: "real", nullable: false),
                    Danceability = table.Column<float>(type: "real", nullable: false),
                    Energy = table.Column<float>(type: "real", nullable: false),
                    Instrumentalness = table.Column<float>(type: "real", nullable: false),
                    Key = table.Column<int>(type: "integer", nullable: false),
                    Liveness = table.Column<float>(type: "real", nullable: false),
                    Loudness = table.Column<float>(type: "real", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Speechiness = table.Column<float>(type: "real", nullable: false),
                    Tempo = table.Column<float>(type: "real", nullable: false),
                    TimeSignature = table.Column<int>(type: "integer", nullable: false),
                    Valence = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_features", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_audio_features_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaylistId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_playlist_tracks_playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlist_tracks_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_albums",
                columns: table => new
                {
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AlbumId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackNumber = table.Column<int>(type: "integer", nullable: false),
                    DiscNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_albums", x => new { x.TrackId, x.AlbumId });
                    table.ForeignKey(
                        name: "FK_track_albums_albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_track_albums_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_artists",
                columns: table => new
                {
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArtistId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_artists", x => new { x.TrackId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_track_artists_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_track_artists_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_albums_Name",
                table: "albums",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_albums_ReleaseDate",
                table: "albums",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_artists_Name",
                table: "artists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_audio_features_Danceability",
                table: "audio_features",
                column: "Danceability");

            migrationBuilder.CreateIndex(
                name: "IX_audio_features_Energy",
                table: "audio_features",
                column: "Energy");

            migrationBuilder.CreateIndex(
                name: "IX_audio_features_Key",
                table: "audio_features",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_audio_features_Tempo",
                table: "audio_features",
                column: "Tempo");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_tracks_AddedAt",
                table: "playlist_tracks",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_tracks_PlaylistId_Position",
                table: "playlist_tracks",
                columns: new[] { "PlaylistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_playlist_tracks_TrackId",
                table: "playlist_tracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_playlists_OwnerId",
                table: "playlists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_playlists_SnapshotId",
                table: "playlists",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_spotify_tokens_UserIdentifier",
                table: "spotify_tokens",
                column: "UserIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_StartedAt",
                table: "sync_history",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_Status",
                table: "sync_history",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sync_history_SyncType",
                table: "sync_history",
                column: "SyncType");

            migrationBuilder.CreateIndex(
                name: "IX_track_albums_AlbumId",
                table: "track_albums",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_track_artists_ArtistId",
                table: "track_artists",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_track_artists_Position",
                table: "track_artists",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_AddedAt",
                table: "tracks",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_Name",
                table: "tracks",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_features");

            migrationBuilder.DropTable(
                name: "playlist_tracks");

            migrationBuilder.DropTable(
                name: "spotify_tokens");

            migrationBuilder.DropTable(
                name: "sync_history");

            migrationBuilder.DropTable(
                name: "track_albums");

            migrationBuilder.DropTable(
                name: "track_artists");

            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "tracks");
        }
    }
}
