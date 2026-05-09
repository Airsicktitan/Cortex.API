using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationConnectionLastTestMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastConnectionTestMode",
                table: "IntegrationConnections",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastConnectionTestMode",
                table: "IntegrationConnections");
        }
    }
}
