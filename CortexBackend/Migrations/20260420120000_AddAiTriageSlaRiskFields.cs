using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public class AddAiTriageSlaRiskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiTriagePotentialSlaRisk",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiTriageSlaRiskReason",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiTriagePotentialSlaRisk",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiTriageSlaRiskReason",
                table: "Tickets");
        }
    }
}
