using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationConnectionHealthLastTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastConnectionTestAtUtc",
                table: "IntegrationConnections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastConnectionTestHealthStatus",
                table: "IntegrationConnections",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastConnectionTestMessage",
                table: "IntegrationConnections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastConnectionTestAtUtc",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "LastConnectionTestHealthStatus",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "LastConnectionTestMessage",
                table: "IntegrationConnections");
        }
    }
}
