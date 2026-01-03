using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistFieldsToSavedCluster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "playlist_created_at",
                table: "saved_clusters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "spotify_playlist_id",
                table: "saved_clusters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "playlist_created_at",
                table: "saved_clusters");

            migrationBuilder.DropColumn(
                name: "spotify_playlist_id",
                table: "saved_clusters");
        }
    }
}
