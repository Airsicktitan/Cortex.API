using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHttpRequestLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HttpRequestLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<double>(type: "float", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsAuthenticated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HttpRequestLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HttpRequestLogEntries_OccurredUtc",
                table: "HttpRequestLogEntries",
                column: "OccurredUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HttpRequestLogEntries");
        }
    }
}
