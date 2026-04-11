using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivedTicketChildrenAndArchiveAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivedComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginalCommentId = table.Column<int>(type: "int", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedComments_ArchivedTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ArchivedTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivedComments_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedTicketAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginalAttachmentId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedBy = table.Column<int>(type: "int", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedTicketAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedTicketAttachments_ArchivedTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ArchivedTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivedTicketAttachments_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedComments_CreatedBy",
                table: "ArchivedComments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedComments_TicketId",
                table: "ArchivedComments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketAttachments_TicketId",
                table: "ArchivedTicketAttachments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTicketAttachments_UploadedBy",
                table: "ArchivedTicketAttachments",
                column: "UploadedBy");

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

            migrationBuilder.DropTable(
                name: "ArchivedComments");

            migrationBuilder.DropTable(
                name: "ArchivedTicketAttachments");
        }
    }
}
