using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAiTriagePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiTriageMissingDetailsJson",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiTriagePriorityReason",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiTriageSuggestedPriority",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiTriageSummary",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiTriageMissingDetailsJson",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiTriagePriorityReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiTriageSuggestedPriority",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiTriageSummary",
                table: "Tickets");
        }
    }
}
