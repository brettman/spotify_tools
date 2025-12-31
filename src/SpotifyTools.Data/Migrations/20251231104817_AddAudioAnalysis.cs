using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpotifyTools.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audio_analyses",
                columns: table => new
                {
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackTempo = table.Column<float>(type: "real", nullable: false),
                    TrackKey = table.Column<int>(type: "integer", nullable: false),
                    TrackMode = table.Column<int>(type: "integer", nullable: false),
                    TrackTimeSignature = table.Column<int>(type: "integer", nullable: false),
                    TrackLoudness = table.Column<float>(type: "real", nullable: false),
                    Duration = table.Column<float>(type: "real", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_analyses", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_audio_analyses_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audio_analysis_sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Start = table.Column<float>(type: "real", nullable: false),
                    Duration = table.Column<float>(type: "real", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    Loudness = table.Column<float>(type: "real", nullable: false),
                    Tempo = table.Column<float>(type: "real", nullable: false),
                    TempoConfidence = table.Column<float>(type: "real", nullable: false),
                    Key = table.Column<int>(type: "integer", nullable: false),
                    KeyConfidence = table.Column<float>(type: "real", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ModeConfidence = table.Column<float>(type: "real", nullable: false),
                    TimeSignature = table.Column<int>(type: "integer", nullable: false),
                    TimeSignatureConfidence = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_analysis_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_analysis_sections_audio_analyses_TrackId",
                        column: x => x.TrackId,
                        principalTable: "audio_analyses",
                        principalColumn: "TrackId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audio_analyses_FetchedAt",
                table: "audio_analyses",
                column: "FetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audio_analysis_sections_Key",
                table: "audio_analysis_sections",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_audio_analysis_sections_Tempo",
                table: "audio_analysis_sections",
                column: "Tempo");

            migrationBuilder.CreateIndex(
                name: "IX_audio_analysis_sections_TimeSignature",
                table: "audio_analysis_sections",
                column: "TimeSignature");

            migrationBuilder.CreateIndex(
                name: "IX_audio_analysis_sections_TrackId",
                table: "audio_analysis_sections",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_analysis_sections");

            migrationBuilder.DropTable(
                name: "audio_analyses");
        }
    }
}
