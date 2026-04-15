using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class EnsureArchiveTicketPersistsBoardAndStoryPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.ArchiveTicket
                    @TicketId nvarchar(450),
                    @ArchivedBy int
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        IF EXISTS (SELECT 1 FROM dbo.ArchivedTickets WHERE Id = @TicketId)
                        BEGIN
                            THROW 50002, 'Ticket is already archived.', 1;
                        END;

                        INSERT INTO dbo.ArchivedTickets
                        (
                            Id,
                            Title,
                            Description,
                            Status,
                            Priority,
                            BoardId,
                            StoryPoints,
                            SynitiOwner,
                            BusinessOwner,
                            CreatedBy,
                            CreatedDate,
                            LastModifiedBy,
                            LastModifiedDate,
                            ArchivedBy,
                            ArchivedDate,
                            CommentCount,
                            AttachmentCount
                        )
                        SELECT
                            t.Id,
                            t.Title,
                            t.Description,
                            t.Status,
                            t.Priority,
                            t.BoardId,
                            t.StoryPoints,
                            t.SynitiOwner,
                            t.BusinessOwner,
                            t.CreatedBy,
                            t.CreatedDate,
                            t.LastModifiedBy,
                            t.LastModifiedDate,
                            @ArchivedBy,
                            SYSUTCDATETIME(),
                            (SELECT COUNT(1) FROM dbo.Comments c WHERE c.TicketId = t.Id),
                            (SELECT COUNT(1) FROM dbo.TicketAttachments a WHERE a.TicketId = t.Id)
                        FROM dbo.Tickets t
                        WHERE t.Id = @TicketId;

                        IF @@ROWCOUNT = 0
                        BEGIN
                            THROW 50001, 'Ticket was not found.', 1;
                        END;

                        INSERT INTO dbo.ArchivedComments
                        (
                            TicketId,
                            OriginalCommentId,
                            Body,
                            CreatedBy,
                            CreatedDate,
                            LastModifiedDate
                        )
                        SELECT
                            c.TicketId,
                            c.Id,
                            c.Body,
                            c.CreatedBy,
                            c.CreatedDate,
                            c.LastModifiedDate
                        FROM dbo.Comments c
                        WHERE c.TicketId = @TicketId;

                        INSERT INTO dbo.ArchivedTicketAttachments
                        (
                            TicketId,
                            OriginalAttachmentId,
                            FileName,
                            ContentType,
                            FileSize,
                            Content,
                            UploadedBy,
                            UploadedDate
                        )
                        SELECT
                            a.TicketId,
                            a.Id,
                            a.FileName,
                            a.ContentType,
                            a.FileSize,
                            a.Content,
                            a.UploadedBy,
                            a.UploadedDate
                        FROM dbo.TicketAttachments a
                        WHERE a.TicketId = @TicketId;

                        DELETE FROM dbo.Tickets
                        WHERE Id = @TicketId;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0
                        BEGIN
                            ROLLBACK TRANSACTION;
                        END;

                        THROW;
                    END CATCH;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op. Rolling back would risk reintroducing
            // a procedure definition that omits BoardId/StoryPoints persistence.
        }
    }
}
