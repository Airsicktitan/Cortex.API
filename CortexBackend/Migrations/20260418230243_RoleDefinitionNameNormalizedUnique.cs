using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class RoleDefinitionNameNormalizedUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoleDefinitions_Name",
                table: "RoleDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "RoleDefinitions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE RoleDefinitions
                SET NameNormalized = UPPER(LTRIM(RTRIM([Name])))
                WHERE NameNormalized IS NULL
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NameNormalized",
                table: "RoleDefinitions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleDefinitions_Name",
                table: "RoleDefinitions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RoleDefinitions_NameNormalized",
                table: "RoleDefinitions",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoleDefinitions_Name",
                table: "RoleDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_RoleDefinitions_NameNormalized",
                table: "RoleDefinitions");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "RoleDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_RoleDefinitions_Name",
                table: "RoleDefinitions",
                column: "Name",
                unique: true);
        }
    }
}
