using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Trace.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Site = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VRefCruKmh = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pilots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNo = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pilots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionClasses_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Days",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayNo = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Days_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gliders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompNo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Registration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Handicap = table.Column<double>(type: "double precision", nullable: false),
                    Icao = table.Column<int>(type: "integer", nullable: true),
                    CompetitionClassId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gliders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gliders_CompetitionClasses_CompetitionClassId",
                        column: x => x.CompetitionClassId,
                        principalTable: "CompetitionClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DayId = table.Column<int>(type: "integer", nullable: false),
                    CompetitionClassId = table.Column<int>(type: "integer", nullable: false),
                    WindDirDeg = table.Column<double>(type: "double precision", nullable: true),
                    WindSpeedKmh = table.Column<double>(type: "double precision", nullable: true),
                    RefHandicap = table.Column<double>(type: "double precision", nullable: true),
                    DRefKm = table.Column<double>(type: "double precision", nullable: true),
                    TRefSec = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_CompetitionClasses_CompetitionClassId",
                        column: x => x.CompetitionClassId,
                        principalTable: "CompetitionClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Days_DayId",
                        column: x => x.DayId,
                        principalTable: "Days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionClassId = table.Column<int>(type: "integer", nullable: false),
                    PilotId = table.Column<int>(type: "integer", nullable: false),
                    GliderId = table.Column<int>(type: "integer", nullable: false),
                    P2PilotId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionEntries_CompetitionClasses_CompetitionClassId",
                        column: x => x.CompetitionClassId,
                        principalTable: "CompetitionClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionEntries_Gliders_GliderId",
                        column: x => x.GliderId,
                        principalTable: "Gliders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionEntries_Pilots_P2PilotId",
                        column: x => x.P2PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionEntries_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Loggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LoggerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GliderId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loggers_Gliders_GliderId",
                        column: x => x.GliderId,
                        principalTable: "Gliders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BarrelRadii",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Handicap = table.Column<double>(type: "double precision", nullable: false),
                    TurnpointIndex = table.Column<int>(type: "integer", nullable: false),
                    RadiusKm = table.Column<double>(type: "double precision", nullable: false),
                    CompetitionTaskId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarrelRadii", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BarrelRadii_Tasks_CompetitionTaskId",
                        column: x => x.CompetitionTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DayEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayId = table.Column<int>(type: "integer", nullable: false),
                    CompetitionClassId = table.Column<int>(type: "integer", nullable: false),
                    PilotId = table.Column<int>(type: "integer", nullable: false),
                    GliderId = table.Column<int>(type: "integer", nullable: false),
                    CompetitionTaskId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayEntries_CompetitionClasses_CompetitionClassId",
                        column: x => x.CompetitionClassId,
                        principalTable: "CompetitionClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DayEntries_Days_DayId",
                        column: x => x.DayId,
                        principalTable: "Days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DayEntries_Gliders_GliderId",
                        column: x => x.GliderId,
                        principalTable: "Gliders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DayEntries_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DayEntries_Tasks_CompetitionTaskId",
                        column: x => x.CompetitionTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Turnpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Waypoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    IsCheckpoint = table.Column<bool>(type: "boolean", nullable: false),
                    IsLine = table.Column<bool>(type: "boolean", nullable: false),
                    Style = table.Column<int>(type: "integer", nullable: false),
                    DirectionType = table.Column<int>(type: "integer", nullable: false),
                    Radius1 = table.Column<double>(type: "double precision", nullable: false),
                    Angle1 = table.Column<double>(type: "double precision", nullable: false),
                    Radius2 = table.Column<double>(type: "double precision", nullable: false),
                    Angle2 = table.Column<double>(type: "double precision", nullable: false),
                    CompetitionTaskId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnpoints_Tasks_CompetitionTaskId",
                        column: x => x.CompetitionTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayEntryId = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Finish = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Speed = table.Column<double>(type: "double precision", nullable: true),
                    Distance = table.Column<double>(type: "double precision", nullable: true),
                    AirspaceValid = table.Column<bool>(type: "boolean", nullable: false),
                    AirspaceChecked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flights_DayEntries_DayEntryId",
                        column: x => x.DayEntryId,
                        principalTable: "DayEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgcFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FlightId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgcFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IgcFiles_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BarrelRadii_CompetitionTaskId_Handicap_TurnpointIndex",
                table: "BarrelRadii",
                columns: new[] { "CompetitionTaskId", "Handicap", "TurnpointIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionClasses_CompetitionId_Name",
                table: "CompetitionClasses",
                columns: new[] { "CompetitionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_CompetitionClassId_GliderId",
                table: "CompetitionEntries",
                columns: new[] { "CompetitionClassId", "GliderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_GliderId",
                table: "CompetitionEntries",
                column: "GliderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_P2PilotId",
                table: "CompetitionEntries",
                column: "P2PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntries_PilotId",
                table: "CompetitionEntries",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_IsActive",
                table: "Competitions",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_Name",
                table: "Competitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_CompetitionClassId",
                table: "DayEntries",
                column: "CompetitionClassId");

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_CompetitionTaskId",
                table: "DayEntries",
                column: "CompetitionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_DayId_CompetitionClassId_GliderId",
                table: "DayEntries",
                columns: new[] { "DayId", "CompetitionClassId", "GliderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_GliderId",
                table: "DayEntries",
                column: "GliderId");

            migrationBuilder.CreateIndex(
                name: "IX_DayEntries_PilotId",
                table: "DayEntries",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Days_CompetitionId_DayNo",
                table: "Days",
                columns: new[] { "CompetitionId", "DayNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flights_DayEntryId",
                table: "Flights",
                column: "DayEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gliders_CompetitionClassId_CompNo",
                table: "Gliders",
                columns: new[] { "CompetitionClassId", "CompNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgcFiles_FlightId",
                table: "IgcFiles",
                column: "FlightId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loggers_GliderId",
                table: "Loggers",
                column: "GliderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CompetitionClassId",
                table: "Tasks",
                column: "CompetitionClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DayId_CompetitionClassId_Active",
                table: "Tasks",
                columns: new[] { "DayId", "CompetitionClassId", "Active" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Turnpoints_CompetitionTaskId_Index",
                table: "Turnpoints",
                columns: new[] { "CompetitionTaskId", "Index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BarrelRadii");

            migrationBuilder.DropTable(
                name: "CompetitionEntries");

            migrationBuilder.DropTable(
                name: "IgcFiles");

            migrationBuilder.DropTable(
                name: "Loggers");

            migrationBuilder.DropTable(
                name: "Turnpoints");

            migrationBuilder.DropTable(
                name: "Flights");

            migrationBuilder.DropTable(
                name: "DayEntries");

            migrationBuilder.DropTable(
                name: "Gliders");

            migrationBuilder.DropTable(
                name: "Pilots");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "CompetitionClasses");

            migrationBuilder.DropTable(
                name: "Days");

            migrationBuilder.DropTable(
                name: "Competitions");
        }
    }
}
