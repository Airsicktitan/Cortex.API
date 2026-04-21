using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionImpactSnapshotToRoutingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionImpactAppliedAtUtc",
                table: "TicketRoutingOverrides",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionImpactAssignmentField",
                table: "TicketRoutingOverrides",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionImpactPreviousOwnerId",
                table: "TicketRoutingOverrides",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionImpactPreviousOwnerWorkload",
                table: "TicketRoutingOverrides",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionImpactPreviousPressureLevel",
                table: "TicketRoutingOverrides",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionImpactPreviousRiskLevel",
                table: "TicketRoutingOverrides",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionImpactPreviousSlaStatus",
                table: "TicketRoutingOverrides",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionImpactSource",
                table: "TicketRoutingOverrides",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionImpactAppliedAtUtc",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactAssignmentField",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactPreviousOwnerId",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactPreviousOwnerWorkload",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactPreviousPressureLevel",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactPreviousRiskLevel",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactPreviousSlaStatus",
                table: "TicketRoutingOverrides");

            migrationBuilder.DropColumn(
                name: "DecisionImpactSource",
                table: "TicketRoutingOverrides");
        }
    }
}
