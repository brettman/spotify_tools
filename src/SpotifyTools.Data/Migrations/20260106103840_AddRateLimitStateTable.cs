using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sync_states_entity_type_phase",
                table: "sync_states");

            migrationBuilder.DropIndex(
                name: "ix_sync_states_is_complete",
                table: "sync_states");

            migrationBuilder.DropIndex(
                name: "ix_sync_states_rate_limit_reset_at",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "is_complete",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "phase",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "rate_limit_hit_at",
                table: "sync_states");

            migrationBuilder.RenameColumn(
                name: "total_estimated",
                table: "sync_states",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "started_at",
                table: "sync_states",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "rate_limit_remaining",
                table: "sync_states",
                newName: "total_items");

            migrationBuilder.RenameColumn(
                name: "last_updated_at",
                table: "sync_states",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "last_synced_offset",
                table: "sync_states",
                newName: "items_processed");

            migrationBuilder.AddColumn<int>(
                name: "current_offset",
                table: "sync_states",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "sync_states",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state_key",
                table: "sync_states",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_entity_type",
                table: "sync_states",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_state_key",
                table: "sync_states",
                column: "state_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_status",
                table: "sync_states",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sync_states_EntityType",
                table: "sync_states");

            migrationBuilder.DropIndex(
                name: "IX_sync_states_StateKey",
                table: "sync_states");

            migrationBuilder.DropIndex(
                name: "IX_sync_states_Status",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "current_offset",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "sync_states");

            migrationBuilder.DropColumn(
                name: "state_key",
                table: "sync_states");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "sync_states",
                newName: "started_at");

            migrationBuilder.RenameColumn(
                name: "total_items",
                table: "sync_states",
                newName: "rate_limit_remaining");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "sync_states",
                newName: "total_estimated");

            migrationBuilder.RenameColumn(
                name: "items_processed",
                table: "sync_states",
                newName: "last_synced_offset");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "sync_states",
                newName: "last_updated_at");

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "sync_states",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_complete",
                table: "sync_states",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "sync_states",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "rate_limit_hit_at",
                table: "sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_entity_type_phase",
                table: "sync_states",
                columns: new[] { "entity_type", "phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_is_complete",
                table: "sync_states",
                column: "is_complete");

            migrationBuilder.CreateIndex(
                name: "ix_sync_states_rate_limit_reset_at",
                table: "sync_states",
                column: "rate_limit_reset_at");
        }
    }
}
