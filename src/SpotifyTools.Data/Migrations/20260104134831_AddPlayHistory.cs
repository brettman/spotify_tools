using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_history",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    track_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    context_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    context_uri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_play_history_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_play_history_context_type",
                table: "play_history",
                column: "context_type");

            migrationBuilder.CreateIndex(
                name: "IX_play_history_played_at",
                table: "play_history",
                column: "played_at");

            migrationBuilder.CreateIndex(
                name: "IX_play_history_track_id_played_at",
                table: "play_history",
                columns: new[] { "track_id", "played_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_history");
        }
    }
}
