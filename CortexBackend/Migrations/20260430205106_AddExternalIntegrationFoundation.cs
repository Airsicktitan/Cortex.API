using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIntegrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuthMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SyncMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastSyncMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalWorkSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationConnectionId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalSourceId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExternalUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalWorkSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalWorkSources_IntegrationConnections_IntegrationConnectionId",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalBoardMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalWorkSourceId = table.Column<int>(type: "int", nullable: false),
                    BoardId = table.Column<int>(type: "int", nullable: false),
                    MappingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalBoardMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalBoardMappings_ExternalWorkSources_ExternalWorkSourceId",
                        column: x => x.ExternalWorkSourceId,
                        principalTable: "ExternalWorkSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalBoardMappings_TicketBoardDefinitions_BoardId",
                        column: x => x.BoardId,
                        principalTable: "TicketBoardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalFieldMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalWorkSourceId = table.Column<int>(type: "int", nullable: false),
                    ExternalFieldName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalFieldKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CortexField = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    TransformHint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalFieldMappings_ExternalWorkSources_ExternalWorkSourceId",
                        column: x => x.ExternalWorkSourceId,
                        principalTable: "ExternalWorkSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalWorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalWorkSourceId = table.Column<int>(type: "int", nullable: false),
                    ExternalItemId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ExternalUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Requester = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CortexTicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalWorkItems_ExternalWorkSources_ExternalWorkSourceId",
                        column: x => x.ExternalWorkSourceId,
                        principalTable: "ExternalWorkSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalWorkItems_Tickets_CortexTicketId",
                        column: x => x.CortexTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalBoardMappings_BoardId",
                table: "ExternalBoardMappings",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalBoardMappings_ExternalWorkSourceId",
                table: "ExternalBoardMappings",
                column: "ExternalWorkSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFieldMappings_ExternalWorkSourceId",
                table: "ExternalFieldMappings",
                column: "ExternalWorkSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_CortexTicketId",
                table: "ExternalWorkItems",
                column: "CortexTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_ExternalWorkSourceId_ExternalItemId",
                table: "ExternalWorkItems",
                columns: new[] { "ExternalWorkSourceId", "ExternalItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_IsDeleted",
                table: "ExternalWorkItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_LastSeenUtc",
                table: "ExternalWorkItems",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkSources_IntegrationConnectionId",
                table: "ExternalWorkSources",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkSources_IntegrationConnectionId_ExternalSourceId",
                table: "ExternalWorkSources",
                columns: new[] { "IntegrationConnectionId", "ExternalSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnections_Provider",
                table: "IntegrationConnections",
                column: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalBoardMappings");

            migrationBuilder.DropTable(
                name: "ExternalFieldMappings");

            migrationBuilder.DropTable(
                name: "ExternalWorkItems");

            migrationBuilder.DropTable(
                name: "ExternalWorkSources");

            migrationBuilder.DropTable(
                name: "IntegrationConnections");
        }
    }
}
