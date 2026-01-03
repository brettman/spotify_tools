using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedClustersTableSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_clusters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    genres = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    primary_genre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_auto_generated = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_finalized = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_clusters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saved_clusters_created_at",
                table: "saved_clusters",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_saved_clusters_is_finalized",
                table: "saved_clusters",
                column: "is_finalized");

            migrationBuilder.CreateIndex(
                name: "ix_saved_clusters_name",
                table: "saved_clusters",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_clusters");
        }
    }
}
