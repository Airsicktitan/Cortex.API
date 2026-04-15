using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketIdSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a database SEQUENCE to generate ticket IDs atomically.
            //
            // Seeds from the current maximum numeric ID across both active and archived
            // tickets so that no ID already in the database is reissued after the
            // migration runs.  Dynamic SQL is required because CREATE SEQUENCE does not
            // accept a variable in its START WITH clause.
            //
            // The CACHE 10 option lets SQL Server pre-allocate blocks of 10 values at a
            // time (faster than NO CACHE).  Gaps of up to 10 can appear after a server
            // restart, which is acceptable for ticket IDs.
            migrationBuilder.Sql(
                """
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS [dbo].[TicketIdSequence];");
        }
    }
}
