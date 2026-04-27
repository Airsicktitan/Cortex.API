using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCortexAutonomyConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CortexAutonomyConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ShadowMode = table.Column<bool>(type: "bit", nullable: false),
                    MinConfidence = table.Column<double>(type: "float", nullable: false),
                    RecentOverrideWindowHours = table.Column<int>(type: "int", nullable: false),
                    RequireClearWinner = table.Column<bool>(type: "bit", nullable: false),
                    MinAlternativeGap = table.Column<double>(type: "float", nullable: false),
                    LastModifiedBy = table.Column<int>(type: "int", nullable: true),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CortexAutonomyConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CortexAutonomyConfigurations_Users_LastModifiedBy",
                        column: x => x.LastModifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CortexAutonomyConfigurations_LastModifiedBy",
                table: "CortexAutonomyConfigurations",
                column: "LastModifiedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CortexAutonomyConfigurations");
        }
    }
}
