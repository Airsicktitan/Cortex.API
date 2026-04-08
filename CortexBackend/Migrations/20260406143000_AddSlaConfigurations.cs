using Cortex.API.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    [DbContext(typeof(CortexDbContext))]
    [Migration("20260406143000_AddSlaConfigurations")]
    public partial class AddSlaConfigurations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaConfigurations",
                columns: table => new
                {
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetHours = table.Column<int>(type: "int", nullable: false),
                    WarningHours = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaConfigurations", x => x.Priority);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaConfigurations");
        }
    }
}
