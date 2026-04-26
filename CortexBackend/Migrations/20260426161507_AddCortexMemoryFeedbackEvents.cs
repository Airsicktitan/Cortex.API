using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCortexMemoryFeedbackEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CortexMemoryFeedbackEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RelatedTicketId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CortexMemoryFeedbackEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CortexMemoryFeedbackEvents_CreatedAtUtc",
                table: "CortexMemoryFeedbackEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CortexMemoryFeedbackEvents_TicketId_EventType",
                table: "CortexMemoryFeedbackEvents",
                columns: new[] { "TicketId", "EventType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CortexMemoryFeedbackEvents");
        }
    }
}
