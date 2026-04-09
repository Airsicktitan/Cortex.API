using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobsAndStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredProcedureDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcedureName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredProcedureDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    JobType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StoredProcedureDefinitionId = table.Column<int>(type: "int", nullable: true),
                    RunAsUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastRunMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobs_StoredProcedureDefinitions_StoredProcedureDefinitionId",
                        column: x => x.StoredProcedureDefinitionId,
                        principalTable: "StoredProcedureDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledJobs_Users_RunAsUserId",
                        column: x => x.RunAsUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_Name",
                table: "ScheduledJobs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_NextRunDateUtc",
                table: "ScheduledJobs",
                column: "NextRunDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_RunAsUserId",
                table: "ScheduledJobs",
                column: "RunAsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_StoredProcedureDefinitionId",
                table: "ScheduledJobs",
                column: "StoredProcedureDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredProcedureDefinitions_Name",
                table: "StoredProcedureDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredProcedureDefinitions_ProcedureName",
                table: "StoredProcedureDefinitions",
                column: "ProcedureName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropTable(
                name: "StoredProcedureDefinitions");
        }
    }
}
