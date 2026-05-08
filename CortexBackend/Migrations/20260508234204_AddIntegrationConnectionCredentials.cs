using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationConnectionCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationConnectionCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationConnectionId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProtectedPayload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SecretKeysJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AuthModeSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnectionCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationConnectionCredentials_IntegrationConnections_IntegrationConnectionId",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnectionCredentials_IntegrationConnectionId",
                table: "IntegrationConnectionCredentials",
                column: "IntegrationConnectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationConnectionCredentials");
        }
    }
}
