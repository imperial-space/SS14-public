using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class NrpResolvesRpAndNrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "resolves",
                table: "nrp_resolves",
                newName: "rp");

            migrationBuilder.AddColumn<int>(
                name: "nrp",
                table: "nrp_resolves",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nrp",
                table: "nrp_resolves");

            migrationBuilder.RenameColumn(
                name: "rp",
                table: "nrp_resolves",
                newName: "resolves");
        }
    }
}
