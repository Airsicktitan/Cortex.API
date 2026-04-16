using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class EnsureTicketIdSequenceExists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[TicketIdSequence]', N'SO') IS NULL
                BEGIN
                    DECLARE @maxId bigint;
                    SELECT @maxId = ISNULL(MAX(
                        CASE
                            WHEN TRY_CAST(Id AS bigint) IS NOT NULL
                                THEN TRY_CAST(Id AS bigint)
                            WHEN Id LIKE 'TICKET-%'
                                THEN TRY_CAST(SUBSTRING(Id, 8, LEN(Id)) AS bigint)
                            ELSE NULL
                        END
                    ), 0)
                    FROM (
                        SELECT Id FROM Tickets
                        UNION ALL
                        SELECT Id FROM ArchivedTickets
                    ) AS AllIds;

                    DECLARE @startWith bigint = @maxId + 1;
                    DECLARE @sql nvarchar(500) =
                        N'CREATE SEQUENCE [dbo].[TicketIdSequence]
                          AS bigint
                          START WITH ' + CAST(@startWith AS nvarchar(20)) + N'
                          INCREMENT BY 1
                          MINVALUE 1
                          NO MAXVALUE
                          NO CYCLE
                          CACHE 10;';
                    EXEC sp_executesql @sql;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS [dbo].[TicketIdSequence];");
        }
    }
}
