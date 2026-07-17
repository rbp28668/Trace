using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Trace.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntryPilotRosterAndDayP2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the new roster table first so existing pilots can be copied
            // across before the old single-pilot columns are dropped.
            migrationBuilder.CreateTable(
                name: "EntryPilots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionEntryId = table.Column<int>(type: "integer", nullable: false),
                    PilotId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryPilots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryPilots_CompetitionEntries_CompetitionEntryId",
                        column: x => x.CompetitionEntryId,
                        principalTable: "CompetitionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntryPilots_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryPilots_CompetitionEntryId_Order",
                table: "EntryPilots",
                columns: new[] { "CompetitionEntryId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryPilots_CompetitionEntryId_PilotId",
                table: "EntryPilots",
                columns: new[] { "CompetitionEntryId", "PilotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryPilots_PilotId",
                table: "EntryPilots",
                column: "PilotId");

            // Copy existing entry pilots into the roster: old primary → order 0,
            // old P2 → order 1 (only where present).
            migrationBuilder.Sql(
                """
                INSERT INTO "EntryPilots" ("CompetitionEntryId", "PilotId", "Order")
                SELECT "Id", "PilotId", 0 FROM "CompetitionEntries";
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "EntryPilots" ("CompetitionEntryId", "PilotId", "Order")
                SELECT "Id", "P2PilotId", 1 FROM "CompetitionEntries"
                WHERE "P2PilotId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionEntries_Pilots_P2PilotId",
                table: "CompetitionEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionEntries_Pilots_PilotId",
                table: "CompetitionEntries");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionEntries_P2PilotId",
                table: "CompetitionEntries");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionEntries_PilotId",
                table: "CompetitionEntries");

            migrationBuilder.DropColumn(
                name: "P2PilotId",
                table: "CompetitionEntries");

            migrationBuilder.DropColumn(
                name: "PilotId",
                table: "CompetitionEntries");

            migrationBuilder.AddColumn<int>(
                name: "P2PilotId",
                table: "DayEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_P2PilotId",
                table: "DayEntries",
                column: "P2PilotId");

            migrationBuilder.AddForeignKey(
                name: "FK_DayEntries_Pilots_P2PilotId",
                table: "DayEntries",
                column: "P2PilotId",
                principalTable: "Pilots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayEntries_Pilots_P2PilotId",
                table: "DayEntries");

            migrationBuilder.DropTable(
                name: "EntryPilots");

            migrationBuilder.DropIndex(
                name: "IX_DayEntries_P2PilotId",
                table: "DayEntries");

            migrationBuilder.DropColumn(
                name: "P2PilotId",
                table: "DayEntries");

            migrationBuilder.AddColumn<int>(
                name: "P2PilotId",
                table: "CompetitionEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PilotId",
                table: "CompetitionEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_P2PilotId",
                table: "CompetitionEntries",
                column: "P2PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_PilotId",
                table: "CompetitionEntries",
                column: "PilotId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionEntries_Pilots_P2PilotId",
                table: "CompetitionEntries",
                column: "P2PilotId",
                principalTable: "Pilots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionEntries_Pilots_PilotId",
                table: "CompetitionEntries",
                column: "PilotId",
                principalTable: "Pilots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
