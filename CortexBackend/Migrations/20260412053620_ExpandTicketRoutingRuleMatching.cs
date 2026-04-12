using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTicketRoutingRuleMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_Department",
                table: "TicketRoutingRules");

            migrationBuilder.AlterColumn<string>(
                name: "SynitiOwner",
                table: "TicketRoutingRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "TicketRoutingRules",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<string>(
                name: "BusinessOwner",
                table: "TicketRoutingRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleContains",
                table: "TicketRoutingRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_Department",
                table: "TicketRoutingRules",
                column: "Department");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_Department_TitleContains",
                table: "TicketRoutingRules",
                columns: new[] { "Department", "TitleContains" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_TitleContains",
                table: "TicketRoutingRules",
                column: "TitleContains");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_Department",
                table: "TicketRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_Department_TitleContains",
                table: "TicketRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_TicketRoutingRules_TitleContains",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "BusinessOwner",
                table: "TicketRoutingRules");

            migrationBuilder.DropColumn(
                name: "TitleContains",
                table: "TicketRoutingRules");

            migrationBuilder.AlterColumn<string>(
                name: "SynitiOwner",
                table: "TicketRoutingRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "TicketRoutingRules",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRoutingRules_Department",
                table: "TicketRoutingRules",
                column: "Department",
                unique: true);
        }
    }
}
