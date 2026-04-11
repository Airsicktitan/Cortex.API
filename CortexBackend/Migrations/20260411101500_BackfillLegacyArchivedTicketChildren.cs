using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLegacyArchivedTicketChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.ArchivedTickets', N'U') IS NULL
                    OR OBJECT_ID(N'dbo.ArchivedComments', N'U') IS NULL
                    OR OBJECT_ID(N'dbo.ArchivedTicketAttachments', N'U') IS NULL
                    OR OBJECT_ID(N'dbo.TicketAuditEntries', N'U') IS NULL
                    OR OBJECT_ID(N'dbo.TicketAuditFieldChanges', N'U') IS NULL
                    OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
                BEGIN
                    RETURN;
                END;

                IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = 0)
                BEGIN
                    SET IDENTITY_INSERT dbo.Users ON;

                    INSERT INTO dbo.Users
                    (
                        Id,
                        DisplayName,
                        Email,
                        Role,
                        Department,
                        CreatedDate,
                        LastLoginDate,
                        ExpiryDate,
                        IsActive,
                        Auth0Id,
                        LastModifiedDate
                    )
                    VALUES
                    (
                        0,
                        N'Legacy User',
                        N'legacy-user@local.invalid',
                        N'User',
                        NULL,
                        SYSUTCDATETIME(),
                        NULL,
                        NULL,
                        1,
                        NULL,
                        NULL
                    );

                    SET IDENTITY_INSERT dbo.Users OFF;
                END;

                ;WITH RecoverableComments AS
                (
                    SELECT
                        archived.Id AS TicketId,
                        fieldChange.NewValue AS Body,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.Users users WHERE users.Id = auditEntry.ChangedBy)
                                THEN auditEntry.ChangedBy
                            ELSE 0
                        END AS CreatedBy,
                        auditEntry.ChangedDateUtc AS CreatedDate
                    FROM dbo.ArchivedTickets archived
                    INNER JOIN dbo.TicketAuditEntries auditEntry
                        ON auditEntry.TicketId = archived.Id
                        AND auditEntry.Action = N'CommentAdded'
                    INNER JOIN dbo.TicketAuditFieldChanges fieldChange
                        ON fieldChange.TicketAuditEntryId = auditEntry.Id
                        AND fieldChange.FieldName = N'Comment'
                    WHERE archived.CommentCount > 0
                      AND fieldChange.NewValue IS NOT NULL
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.ArchivedComments archivedComments
                          WHERE archivedComments.TicketId = archived.Id
                      )
                )
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
                    recoverable.TicketId,
                    NULL,
                    recoverable.Body,
                    recoverable.CreatedBy,
                    recoverable.CreatedDate,
                    recoverable.CreatedDate
                FROM RecoverableComments recoverable;

                ;WITH NumberSeries AS
                (
                    SELECT TOP (1024)
                        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number
                    FROM sys.all_objects objectsA
                    CROSS JOIN sys.all_objects objectsB
                ),
                MissingCommentCounts AS
                (
                    SELECT
                        archived.Id AS TicketId,
                        archived.CommentCount,
                        archived.ArchivedDate,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.Users users WHERE users.Id = archived.ArchivedBy)
                                THEN archived.ArchivedBy
                            ELSE 0
                        END AS CreatedBy,
                        ISNULL(existing.CommentRows, 0) AS ExistingCommentRows
                    FROM dbo.ArchivedTickets archived
                    OUTER APPLY
                    (
                        SELECT COUNT(*) AS CommentRows
                        FROM dbo.ArchivedComments archivedComments
                        WHERE archivedComments.TicketId = archived.Id
                    ) existing
                    WHERE archived.CommentCount > ISNULL(existing.CommentRows, 0)
                )
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
                    missing.TicketId,
                    NULL,
                    CONCAT(
                        N'Legacy archived comment ',
                        numbers.Number + missing.ExistingCommentRows,
                        N' could not be fully recovered because this ticket was archived before comment payload preservation was enabled.'
                    ),
                    missing.CreatedBy,
                    missing.ArchivedDate,
                    missing.ArchivedDate
                FROM MissingCommentCounts missing
                INNER JOIN NumberSeries numbers
                    ON numbers.Number <= missing.CommentCount - missing.ExistingCommentRows;

                ;WITH RecoverableAttachments AS
                (
                    SELECT
                        archived.Id AS TicketId,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.Users users WHERE users.Id = auditEntry.ChangedBy)
                                THEN auditEntry.ChangedBy
                            ELSE 0
                        END AS UploadedBy,
                        auditEntry.ChangedDateUtc AS UploadedDate,
                        CASE
                            WHEN LEN(fieldChange.NewValue) > 240
                                THEN LEFT(fieldChange.NewValue, 240) + N'.legacy.txt'
                            ELSE fieldChange.NewValue + N'.legacy.txt'
                        END AS FileName,
                        CAST(
                            N'Legacy archived attachment placeholder. Original file "' +
                            fieldChange.NewValue +
                            N'" was archived before binary attachment preservation was enabled, so the original file contents cannot be recovered.'
                            AS nvarchar(max)
                        ) AS PlaceholderText
                    FROM dbo.ArchivedTickets archived
                    INNER JOIN dbo.TicketAuditEntries auditEntry
                        ON auditEntry.TicketId = archived.Id
                        AND auditEntry.Action = N'AttachmentAdded'
                    INNER JOIN dbo.TicketAuditFieldChanges fieldChange
                        ON fieldChange.TicketAuditEntryId = auditEntry.Id
                        AND fieldChange.FieldName = N'Attachment'
                    WHERE archived.AttachmentCount > 0
                      AND fieldChange.NewValue IS NOT NULL
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.ArchivedTicketAttachments archivedAttachments
                          WHERE archivedAttachments.TicketId = archived.Id
                      )
                )
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
                    recoverable.TicketId,
                    NULL,
                    recoverable.FileName,
                    N'text/plain',
                    DATALENGTH(CONVERT(varbinary(max), recoverable.PlaceholderText)),
                    CONVERT(varbinary(max), recoverable.PlaceholderText),
                    recoverable.UploadedBy,
                    recoverable.UploadedDate
                FROM RecoverableAttachments recoverable;

                ;WITH NumberSeries AS
                (
                    SELECT TOP (1024)
                        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number
                    FROM sys.all_objects objectsA
                    CROSS JOIN sys.all_objects objectsB
                ),
                MissingAttachmentCounts AS
                (
                    SELECT
                        archived.Id AS TicketId,
                        archived.AttachmentCount,
                        archived.ArchivedDate,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.Users users WHERE users.Id = archived.ArchivedBy)
                                THEN archived.ArchivedBy
                            ELSE 0
                        END AS UploadedBy,
                        ISNULL(existing.AttachmentRows, 0) AS ExistingAttachmentRows
                    FROM dbo.ArchivedTickets archived
                    OUTER APPLY
                    (
                        SELECT COUNT(*) AS AttachmentRows
                        FROM dbo.ArchivedTicketAttachments archivedAttachments
                        WHERE archivedAttachments.TicketId = archived.Id
                    ) existing
                    WHERE archived.AttachmentCount > ISNULL(existing.AttachmentRows, 0)
                )
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
                    missing.TicketId,
                    NULL,
                    CONCAT(N'legacy-missing-attachment-', numbers.Number + missing.ExistingAttachmentRows, N'.txt'),
                    N'text/plain',
                    DATALENGTH(
                        CONVERT(
                            varbinary(max),
                            CONCAT(
                                N'Legacy archived attachment ',
                                numbers.Number + missing.ExistingAttachmentRows,
                                N' could not be recovered because this ticket was archived before binary attachment preservation was enabled.'
                            )
                        )
                    ),
                    CONVERT(
                        varbinary(max),
                        CONCAT(
                            N'Legacy archived attachment ',
                            numbers.Number + missing.ExistingAttachmentRows,
                            N' could not be recovered because this ticket was archived before binary attachment preservation was enabled.'
                        )
                    ),
                    missing.UploadedBy,
                    missing.ArchivedDate
                FROM MissingAttachmentCounts missing
                INNER JOIN NumberSeries numbers
                    ON numbers.Number <= missing.AttachmentCount - missing.ExistingAttachmentRows;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op. This migration backfills historical archive data for legacy tickets.
        }
    }
}
