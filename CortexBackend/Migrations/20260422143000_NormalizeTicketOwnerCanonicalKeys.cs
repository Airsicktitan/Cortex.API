using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeTicketOwnerCanonicalKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            NormalizeOwnerColumn(migrationBuilder, "Tickets", "SynitiOwner");
            NormalizeOwnerColumn(migrationBuilder, "Tickets", "BusinessOwner");
            NormalizeOwnerColumn(migrationBuilder, "ArchivedTickets", "SynitiOwner");
            NormalizeOwnerColumn(migrationBuilder, "ArchivedTickets", "BusinessOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingDecisions", "ChosenSynitiOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingDecisions", "ChosenBusinessOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingOverrides", "PreviousSynitiOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingOverrides", "PreviousBusinessOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingOverrides", "NewSynitiOwner");
            NormalizeOwnerColumn(migrationBuilder, "TicketRoutingOverrides", "NewBusinessOwner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data normalization. Original aliases cannot be reconstructed reliably.
        }

        private static void NormalizeOwnerColumn(
            MigrationBuilder migrationBuilder,
            string tableName,
            string columnName)
        {
            migrationBuilder.Sql($"""
                WITH AliasMap AS (
                    SELECT
                        LOWER(LTRIM(RTRIM(CONCAT('user:', CAST([Id] AS nvarchar(20)))))) AS NormalizedOwner,
                        CONCAT('user:', CAST([Id] AS nvarchar(20))) AS CanonicalOwner
                    FROM [Users]
                    UNION ALL
                    SELECT
                        LOWER(LTRIM(RTRIM([Email]))) AS NormalizedOwner,
                        CONCAT('user:', CAST([Id] AS nvarchar(20))) AS CanonicalOwner
                    FROM [Users]
                    WHERE [Email] IS NOT NULL AND LTRIM(RTRIM([Email])) <> ''
                    UNION ALL
                    SELECT
                        LOWER(LTRIM(RTRIM([DisplayName]))) AS NormalizedOwner,
                        CONCAT('user:', CAST([Id] AS nvarchar(20))) AS CanonicalOwner
                    FROM [Users]
                    WHERE [DisplayName] IS NOT NULL AND LTRIM(RTRIM([DisplayName])) <> ''
                    UNION ALL
                    SELECT
                        LOWER(LTRIM(RTRIM([NickName]))) AS NormalizedOwner,
                        CONCAT('user:', CAST([Id] AS nvarchar(20))) AS CanonicalOwner
                    FROM [Users]
                    WHERE [NickName] IS NOT NULL AND LTRIM(RTRIM([NickName])) <> ''
                ),
                UniqueAlias AS (
                    SELECT
                        NormalizedOwner,
                        MIN(CanonicalOwner) AS CanonicalOwner
                    FROM AliasMap
                    WHERE NormalizedOwner IS NOT NULL AND NormalizedOwner <> ''
                    GROUP BY NormalizedOwner
                    HAVING COUNT(DISTINCT CanonicalOwner) = 1
                )
                UPDATE target
                SET [{columnName}] = alias.CanonicalOwner
                FROM [{tableName}] AS target
                INNER JOIN UniqueAlias AS alias
                    ON LOWER(LTRIM(RTRIM(target.[{columnName}]))) = alias.NormalizedOwner
                WHERE target.[{columnName}] IS NOT NULL
                    AND LTRIM(RTRIM(target.[{columnName}])) <> ''
                    AND target.[{columnName}] <> alias.CanonicalOwner;
                """);
        }
    }
}
