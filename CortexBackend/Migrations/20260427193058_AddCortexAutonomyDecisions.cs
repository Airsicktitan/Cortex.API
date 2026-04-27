using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCortexAutonomyDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CortexAutonomyDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecommendedOwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RecommendedOwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreviousOwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LearningAdjustment = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    WasAutoApplied = table.Column<bool>(type: "bit", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PassedChecksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockedReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DecisionVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CortexAutonomyDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CortexAutonomyDecisions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CortexAutonomyDecisions_TicketId_CreatedDateUtc",
                table: "CortexAutonomyDecisions",
                columns: new[] { "TicketId", "CreatedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CortexAutonomyDecisions_WasAutoApplied",
                table: "CortexAutonomyDecisions",
                column: "WasAutoApplied");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CortexAutonomyDecisions");
        }
    }
}
