using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSynitiKnowledgeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SynitiKnowledgeSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynitiKnowledgeSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SynitiKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SynitiKnowledgeSourceId = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShortDefinition = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    BusinessMeaning = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    TechnicalMeaning = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CommonSignals = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RelatedTerms = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ExamplePhrases = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynitiKnowledgeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SynitiKnowledgeEntries_SynitiKnowledgeSources_SynitiKnowledgeSourceId",
                        column: x => x.SynitiKnowledgeSourceId,
                        principalTable: "SynitiKnowledgeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SynitiKnowledgeEntries_SynitiKnowledgeSourceId",
                table: "SynitiKnowledgeEntries",
                column: "SynitiKnowledgeSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SynitiKnowledgeEntries_SynitiKnowledgeSourceId_Term",
                table: "SynitiKnowledgeEntries",
                columns: new[] { "SynitiKnowledgeSourceId", "Term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SynitiKnowledgeSources_Name",
                table: "SynitiKnowledgeSources",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SynitiKnowledgeEntries");

            migrationBuilder.DropTable(
                name: "SynitiKnowledgeSources");
        }
    }
}
