using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutingEngineV1Persistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardId",
                table: "TicketRoutingRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "TicketRoutingRules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterDepartment",
                table: "TicketRoutingRules",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterRole",
                table: "TicketRoutingRules",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RulePriority",
                table: "TicketRoutingRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                table: "TicketRoutingRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TicketRoutingDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MatchedRuleId = table.Column<int>(type: "int", nullable: true),
                    OutcomeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NoMatchReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ChosenSynitiOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChosenBusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrecedenceScore = table.Column<int>(type: "int", nullable: false),
                    TieBreakKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExplanationText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRoutingDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketRoutingDecisions_TicketRoutingRules_MatchedRuleId",
                        column: x => x.MatchedRuleId,
                        principalTable: "TicketRoutingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketRoutingDecisions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketRoutingOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OverriddenByUserId = table.Column<int>(type: "int", nullable: false),
                    PreviousSynitiOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousBusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewSynitiOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewBusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverrideReasonType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OverrideReasonText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRoutingOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketRoutingOverrides_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketRoutingOverrides_Users_OverriddenByUserId",
                        column: x => x.OverriddenByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_BoardId",
                table: "TicketRoutingRules",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_Priority",
                table: "TicketRoutingRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_RequesterDepartment",
                table: "TicketRoutingRules",
                column: "RequesterDepartment");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_RequesterRole",
                table: "TicketRoutingRules",
                column: "RequesterRole");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingDecisions_MatchedRuleId",
                table: "TicketRoutingDecisions",
                column: "MatchedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingDecisions_TicketId_CreatedDateUtc",
                table: "TicketRoutingDecisions",
                columns: new[] { "TicketId", "CreatedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingOverrides_OverriddenByUserId",
                table: "TicketRoutingOverrides",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingOverrides_TicketId_CreatedDateUtc",
                table: "TicketRoutingOverrides",
                columns: new[] { "TicketId", "CreatedDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketRoutingDecisions");

            migrationBuilder.DropTable(
                name: "TicketRoutingOverrides");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_BoardId",
                table: "TicketRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_Priority",
                table: "TicketRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_RequesterDepartment",
                table: "TicketRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_RequesterRole",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "RequesterDepartment",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "RequesterRole",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "RulePriority",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "TicketRoutingRules");
        }
    }
}
