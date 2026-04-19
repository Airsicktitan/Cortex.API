using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowMetricEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowMetricEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowMetricEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowMetricEvents_EventType",
                table: "WorkflowMetricEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowMetricEvents_OccurredUtc",
                table: "WorkflowMetricEvents",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowMetricEvents_TicketId",
                table: "WorkflowMetricEvents",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowMetricEvents");
        }
    }
}
