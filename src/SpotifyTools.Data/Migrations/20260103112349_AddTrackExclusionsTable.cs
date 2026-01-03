using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackExclusionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "track_exclusions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: false),
                    track_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    excluded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_exclusions", x => x.id);
                    table.ForeignKey(
                        name: "fk_track_exclusions_saved_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "saved_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_track_exclusions_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_track_exclusions_cluster_id_track_id",
                table: "track_exclusions",
                columns: new[] { "cluster_id", "track_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_track_exclusions_excluded_at",
                table: "track_exclusions",
                column: "excluded_at");

            migrationBuilder.CreateIndex(
                name: "ix_track_exclusions_track_id",
                table: "track_exclusions",
                column: "track_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "track_exclusions");
        }
    }
}
