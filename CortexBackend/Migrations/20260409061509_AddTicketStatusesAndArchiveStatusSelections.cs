using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketStatusesAndArchiveStatusSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibleStatusesJson",
                table: "ArchiveConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(
                """
                UPDATE [ArchiveConfigurations]
                SET [EligibleStatusesJson] =
                    CASE
                        WHEN [ArchiveResolvedTickets] = 1 AND [ArchiveClosedTickets] = 1 THEN N'["Resolved","Closed"]'
                        WHEN [ArchiveResolvedTickets] = 1 THEN N'["Resolved"]'
                        WHEN [ArchiveClosedTickets] = 1 THEN N'["Closed"]'
                        ELSE N'[]'
                    END
                """);

            migrationBuilder.DropColumn(
                name: "ArchiveClosedTickets",
                table: "ArchiveConfigurations");

            migrationBuilder.DropColumn(
                name: "ArchiveResolvedTickets",
                table: "ArchiveConfigurations");

            migrationBuilder.CreateTable(
                name: "TicketStatusDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatusDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusDefinitions_Name",
                table: "TicketStatusDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.InsertData(
                table: "TicketStatusDefinitions",
                columns: new[] { "Id", "Name", "Description", "IsEnabled", "CreatedDateUtc", "LastModifiedDateUtc" },
                values: new object[,]
                {
                    { 1, "New", "Recently created work waiting to be picked up.", true, new DateTime(2026, 4, 9, 6, 15, 9, DateTimeKind.Utc), null },
                    { 2, "In Progress", "Active work currently being handled.", true, new DateTime(2026, 4, 9, 6, 15, 9, DateTimeKind.Utc), null },
                    { 3, "Pending Business Review", "Waiting for business validation or feedback.", true, new DateTime(2026, 4, 9, 6, 15, 9, DateTimeKind.Utc), null },
                    { 4, "Resolved", "Technical work is complete and ready for closure or archive.", true, new DateTime(2026, 4, 9, 6, 15, 9, DateTimeKind.Utc), null },
                    { 5, "Closed", "Ticket has been completed and fully closed out.", true, new DateTime(2026, 4, 9, 6, 15, 9, DateTimeKind.Utc), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArchiveClosedTickets",
                table: "ArchiveConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ArchiveResolvedTickets",
                table: "ArchiveConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE [ArchiveConfigurations]
                SET
                    [ArchiveResolvedTickets] = CASE WHEN [EligibleStatusesJson] LIKE '%"Resolved"%' THEN 1 ELSE 0 END,
                    [ArchiveClosedTickets] = CASE WHEN [EligibleStatusesJson] LIKE '%"Closed"%' THEN 1 ELSE 0 END
                """);

            migrationBuilder.DropTable(
                name: "TicketStatusDefinitions");

            migrationBuilder.DropColumn(
                name: "EligibleStatusesJson",
                table: "ArchiveConfigurations");
        }
    }
}
