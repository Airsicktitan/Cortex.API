using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationConnectionId = table.Column<int>(type: "int", nullable: true),
                    ExternalWorkSourceId = table.Column<int>(type: "int", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TriggeredByUserId = table.Column<int>(type: "int", nullable: true),
                    TriggeredByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TriggeredByEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CreatedCount = table.Column<int>(type: "int", nullable: true),
                    UpdatedCount = table.Column<int>(type: "int", nullable: true),
                    UnchangedCount = table.Column<int>(type: "int", nullable: true),
                    SkippedCount = table.Column<int>(type: "int", nullable: true),
                    ErrorCount = table.Column<int>(type: "int", nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationActivityLogs_ExternalWorkSources_ExternalWorkSourceId",
                        column: x => x.ExternalWorkSourceId,
                        principalTable: "ExternalWorkSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegrationActivityLogs_IntegrationConnections_IntegrationConnectionId",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationActivityLogs_ExternalWorkSourceId_StartedAtUtc",
                table: "IntegrationActivityLogs",
                columns: new[] { "ExternalWorkSourceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationActivityLogs_IntegrationConnectionId",
                table: "IntegrationActivityLogs",
                column: "IntegrationConnectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationActivityLogs");
        }
    }
}
