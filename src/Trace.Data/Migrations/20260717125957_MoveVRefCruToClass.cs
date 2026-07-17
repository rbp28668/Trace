using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trace.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveVRefCruToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VRefCruKmh",
                table: "Competitions");

            migrationBuilder.AddColumn<double>(
                name: "VRefCruKmh",
                table: "CompetitionClasses",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VRefCruKmh",
                table: "CompetitionClasses");

            migrationBuilder.AddColumn<double>(
                name: "VRefCruKmh",
                table: "Competitions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
