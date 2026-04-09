using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseBackedReportsAndStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefinitionSql",
                table: "StoredProcedureDefinitions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ViewName",
                table: "ReportDefinitions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [ReportDefinitions]
                SET [ViewName] = CONCAT(N'dbo.vw_CortexReport_', CAST([Id] AS nvarchar(20)))
                WHERE [ViewName] IS NULL OR LTRIM(RTRIM([ViewName])) = N''
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_ViewName",
                table: "ReportDefinitions",
                column: "ViewName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_ViewName",
                table: "ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "DefinitionSql",
                table: "StoredProcedureDefinitions");

            migrationBuilder.DropColumn(
                name: "ViewName",
                table: "ReportDefinitions");
        }
    }
}
