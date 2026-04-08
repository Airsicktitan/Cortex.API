using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivedTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SynitiOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<int>(type: "int", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedBy = table.Column<int>(type: "int", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    AttachmentCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedTickets_Users_ArchivedBy",
                        column: x => x.ArchivedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivedTickets_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_ArchivedBy",
                table: "ArchivedTickets",
                column: "ArchivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_ArchivedDate",
                table: "ArchivedTickets",
                column: "ArchivedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_CreatedBy",
                table: "ArchivedTickets",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_Priority",
                table: "ArchivedTickets",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTickets_Status",
                table: "ArchivedTickets",
                column: "Status");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ArchiveTicket;");

            migrationBuilder.DropTable(
                name: "ArchivedTickets");
        }
    }
}
