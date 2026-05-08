using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationActivityLogOptionalExternalSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationActivityLogs_IntegrationConnectionId",
                table: "IntegrationActivityLogs");

            migrationBuilder.AlterColumn<int>(
                name: "ExternalWorkSourceId",
                table: "IntegrationActivityLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationActivityLogs_IntegrationConnectionId_StartedAtUtc",
                table: "IntegrationActivityLogs",
                columns: new[] { "IntegrationConnectionId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationActivityLogs_IntegrationConnectionId_StartedAtUtc",
                table: "IntegrationActivityLogs");

            migrationBuilder.AlterColumn<int>(
                name: "ExternalWorkSourceId",
                table: "IntegrationActivityLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationActivityLogs_IntegrationConnectionId",
                table: "IntegrationActivityLogs",
                column: "IntegrationConnectionId");
        }
    }
}
