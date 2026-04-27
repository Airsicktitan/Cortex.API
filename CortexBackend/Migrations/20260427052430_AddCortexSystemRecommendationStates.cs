using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCortexSystemRecommendationStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CortexSystemRecommendationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecommendationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DismissedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CortexSystemRecommendationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CortexSystemRecommendationStates_RecommendationId",
                table: "CortexSystemRecommendationStates",
                column: "RecommendationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CortexSystemRecommendationStates_ReviewedAtUtc",
                table: "CortexSystemRecommendationStates",
                column: "ReviewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CortexSystemRecommendationStates_Status",
                table: "CortexSystemRecommendationStates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CortexSystemRecommendationStates");
        }
    }
}
