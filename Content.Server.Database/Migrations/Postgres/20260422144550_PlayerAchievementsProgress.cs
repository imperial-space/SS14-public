using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PlayerAchievementsProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_achievements_progress",
                columns: table => new
                {
                    player_achievements_progress_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achievement_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    progress_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_achievements_progress", x => x.player_achievements_progress_id);
                    table.ForeignKey(
                        name: "FK_player_achievements_progress_player_player_id",
                        column: x => x.player_user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_achievements_progress_player_user_id",
                table: "player_achievements_progress",
                column: "player_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_achievements_progress_player_user_id_achievement_id_~",
                table: "player_achievements_progress",
                columns: new[] { "player_user_id", "achievement_id", "progress_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_achievements_progress");
        }
    }
}
