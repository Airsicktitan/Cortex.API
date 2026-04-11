using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 0)
                BEGIN
                    SET IDENTITY_INSERT [Users] ON;
                    INSERT INTO [Users]
                    (
                        [Id],
                        [DisplayName],
                        [Email],
                        [Role],
                        [Department],
                        [CreatedDate],
                        [LastLoginDate],
                        [ExpiryDate],
                        [IsActive],
                        [Auth0Id],
                        [LastModifiedDate]
                    )
                    VALUES
                    (
                        0,
                        'Legacy User',
                        'legacy-user@local.invalid',
                        'User',
                        NULL,
                        SYSUTCDATETIME(),
                        NULL,
                        NULL,
                        1,
                        NULL,
                        NULL
                    );
                    SET IDENTITY_INSERT [Users] OFF;
                END;

                UPDATE [Comments]
                SET [CreatedBy] = '0'
                WHERE TRY_CONVERT(int, [CreatedBy]) IS NULL
                   OR NOT EXISTS
                   (
                       SELECT 1
                       FROM [Users]
                       WHERE [Id] = TRY_CONVERT(int, [Comments].[CreatedBy])
                   );
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Comments",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE [Comments]
                SET [CreatedBy] = 0
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [Users]
                    WHERE [Id] = [Comments].[CreatedBy]
                );

                UPDATE [Comments]
                SET [UserId] = [CreatedBy];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_UserId",
                table: "Comments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_UserId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_UserId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Comments");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
