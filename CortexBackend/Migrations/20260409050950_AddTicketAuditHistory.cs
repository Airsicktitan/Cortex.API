using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<int>(type: "int", nullable: false),
                    ChangedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAuditEntries_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketAuditFieldChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketAuditEntryId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAuditFieldChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAuditFieldChanges_TicketAuditEntries_TicketAuditEntryId",
                        column: x => x.TicketAuditEntryId,
                        principalTable: "TicketAuditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAuditEntries_ChangedBy",
                table: "TicketAuditEntries",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAuditEntries_TicketId_ChangedDateUtc",
                table: "TicketAuditEntries",
                columns: new[] { "TicketId", "ChangedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAuditFieldChanges_TicketAuditEntryId",
                table: "TicketAuditFieldChanges",
                column: "TicketAuditEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAuditFieldChanges");

            migrationBuilder.DropTable(
                name: "TicketAuditEntries");
        }
    }
}
