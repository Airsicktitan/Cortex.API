using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Tickets",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedBy",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Tickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "Tickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedForDetailAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnedForDetailBy",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ApprovalStatus",
                table: "Tickets",
                column: "ApprovalStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ApprovalStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ReturnedForDetailAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ReturnedForDetailBy",
                table: "Tickets");
        }
    }
}
