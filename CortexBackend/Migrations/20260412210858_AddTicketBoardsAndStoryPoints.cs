using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketBoardsAndStoryPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketBoardDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresStoryPoints = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketBoardDefinitions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TicketBoardDefinitions",
                columns: ["Id", "Name", "Description", "RequiresStoryPoints", "IsEnabled", "CreatedDateUtc", "LastModifiedDateUtc"],
                values: new object[,]
                {
                    { 1, "Ticket", "Standard operational ticket board.", false, true, new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, "Hypercare", "High-touch stabilization and production support work.", false, true, new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, "Enhancement", "Planned improvements and backlog work.", true, true, new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "ArchivedTickets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "ArchivedTickets",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE dbo.Tickets
                SET BoardId = 1
                WHERE BoardId IS NULL OR BoardId = 0;

                UPDATE dbo.ArchivedTickets
                SET BoardId = 1
                WHERE BoardId IS NULL OR BoardId = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BoardId",
                table: "Tickets",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_BoardId",
                table: "ArchivedTickets",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketBoardDefinitions_Name",
                table: "TicketBoardDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivedTickets_TicketBoardDefinitions_BoardId",
                table: "ArchivedTickets",
                column: "BoardId",
                principalTable: "TicketBoardDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketBoardDefinitions_BoardId",
                table: "Tickets",
                column: "BoardId",
                principalTable: "TicketBoardDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                            ISNULL(NULLIF(t.BoardId, 0), 1),
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

            migrationBuilder.DropForeignKey(
                name: "FK_ArchivedTickets_TicketBoardDefinitions_BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketBoardDefinitions_BoardId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketBoardDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_BoardId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_ArchivedTickets_BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "ArchivedTickets");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "ArchivedTickets");
        }
    }
}
