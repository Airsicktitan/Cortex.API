using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketOutcomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BoardId = table.Column<int>(type: "int", nullable: false),
                    AssignedSynitiOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignedBusinessOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FinalSynitiOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FinalBusinessOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WasOverridden = table.Column<bool>(type: "bit", nullable: false),
                    SlaBreached = table.Column<bool>(type: "bit", nullable: false),
                    WasReassigned = table.Column<bool>(type: "bit", nullable: false),
                    WasReopened = table.Column<bool>(type: "bit", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    ReachedTerminalStatus = table.Column<bool>(type: "bit", nullable: false),
                    MatchedRuleId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketOutcomes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_BoardId",
                table: "TicketOutcomes",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_FinalBusinessOwner",
                table: "TicketOutcomes",
                column: "FinalBusinessOwner");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_FinalSynitiOwner",
                table: "TicketOutcomes",
                column: "FinalSynitiOwner");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_MatchedRuleId",
                table: "TicketOutcomes",
                column: "MatchedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_ReachedTerminalStatus",
                table: "TicketOutcomes",
                column: "ReachedTerminalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOutcomes_TicketId",
                table: "TicketOutcomes",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketOutcomes");
        }
    }
}
