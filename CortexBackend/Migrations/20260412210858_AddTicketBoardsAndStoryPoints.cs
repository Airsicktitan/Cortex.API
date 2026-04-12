using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketBoardsAndStoryPoints : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "ArchivedTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "ArchivedTickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketBoardDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresStoryPoints = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketBoardDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BoardId",
                table: "Tickets",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_BoardId",
                table: "ArchivedTickets",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketBoardDefinitions_Name",
                table: "TicketBoardDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivedTickets_TicketBoardDefinitions_BoardId",
                table: "ArchivedTickets",
                column: "BoardId",
                principalTable: "TicketBoardDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketBoardDefinitions_BoardId",
                table: "Tickets",
                column: "BoardId",
                principalTable: "TicketBoardDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivedTickets_TicketBoardDefinitions_BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketBoardDefinitions_BoardId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketBoardDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_BoardId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_ArchivedTickets_BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "ArchivedTickets");
        }
    }
}
