using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSynitiKnowledgeReviewerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "SynitiKnowledgeEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissingContextQuestions",
                table: "SynitiKnowledgeEntries",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedReviewerChecks",
                table: "SynitiKnowledgeEntries",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "SynitiKnowledgeEntries");

            migrationBuilder.DropColumn(
                name: "MissingContextQuestions",
                table: "SynitiKnowledgeEntries");

            migrationBuilder.DropColumn(
                name: "SuggestedReviewerChecks",
                table: "SynitiKnowledgeEntries");
        }
    }
}
