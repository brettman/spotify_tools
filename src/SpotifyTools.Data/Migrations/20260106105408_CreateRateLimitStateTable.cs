using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateRateLimitStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rate_limit_states",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    request_count = table.Column<int>(type: "integer", nullable: false),
                    window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rate_limit_hit_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rate_limit_resets_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_rate_limited = table.Column<bool>(type: "boolean", nullable: false),
                    retry_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_request_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_rate_limit_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_limit_states", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rate_limit_states");
        }
    }
}
