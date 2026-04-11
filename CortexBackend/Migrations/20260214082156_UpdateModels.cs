using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels : Migration
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

                UPDATE [Tickets]
                SET [CreatedBy] = '0'
                WHERE TRY_CONVERT(int, [CreatedBy]) IS NULL
                   OR NOT EXISTS
                   (
                       SELECT 1
                       FROM [Users]
                       WHERE [Id] = TRY_CONVERT(int, [Tickets].[CreatedBy])
                   );

                UPDATE [Tickets]
                SET [LastModifiedBy] = '0'
                WHERE [LastModifiedBy] IS NULL
                   OR TRY_CONVERT(int, [LastModifiedBy]) IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "LastModifiedBy",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Tickets",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql(
                """
                UPDATE [Tickets]
                SET [CreatedBy] = 0
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [Users]
                    WHERE [Id] = [Tickets].[CreatedBy]
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedBy",
                table: "Tickets",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_CreatedBy",
                table: "Tickets",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_CreatedBy",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedBy",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "LastModifiedBy",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
