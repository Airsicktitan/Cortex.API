using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSapReferenceKnowledgeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SapReferenceSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SystemLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Client = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapReferenceSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SapDomainValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SapReferenceSourceId = table.Column<int>(type: "int", nullable: false),
                    DomainName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapDomainValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapDomainValues_SapReferenceSources_SapReferenceSourceId",
                        column: x => x.SapReferenceSourceId,
                        principalTable: "SapReferenceSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SapTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SapReferenceSourceId = table.Column<int>(type: "int", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BusinessObject = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DataDomain = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapTables_SapReferenceSources_SapReferenceSourceId",
                        column: x => x.SapReferenceSourceId,
                        principalTable: "SapReferenceSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SapFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SapTableMetadataId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataElement = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DomainName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DataType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Length = table.Column<int>(type: "int", nullable: true),
                    IsKey = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: true),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    BusinessMeaning = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExampleValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapFields_SapTables_SapTableMetadataId",
                        column: x => x.SapTableMetadataId,
                        principalTable: "SapTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SapDomainValues_SapReferenceSourceId",
                table: "SapDomainValues",
                column: "SapReferenceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SapDomainValues_SapReferenceSourceId_DomainName_Value",
                table: "SapDomainValues",
                columns: new[] { "SapReferenceSourceId", "DomainName", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_DataElement",
                table: "SapFields",
                column: "DataElement");

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_DomainName",
                table: "SapFields",
                column: "DomainName");

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_FieldName",
                table: "SapFields",
                column: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_IsCustom",
                table: "SapFields",
                column: "IsCustom");

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_SapTableMetadataId",
                table: "SapFields",
                column: "SapTableMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_SapFields_SapTableMetadataId_FieldName",
                table: "SapFields",
                columns: new[] { "SapTableMetadataId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SapReferenceSources_Name",
                table: "SapReferenceSources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_BusinessObject",
                table: "SapTables",
                column: "BusinessObject");

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_IsCustom",
                table: "SapTables",
                column: "IsCustom");

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_Module",
                table: "SapTables",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_SapReferenceSourceId",
                table: "SapTables",
                column: "SapReferenceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_SapReferenceSourceId_TableName",
                table: "SapTables",
                columns: new[] { "SapReferenceSourceId", "TableName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SapTables_TableName",
                table: "SapTables",
                column: "TableName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SapDomainValues");

            migrationBuilder.DropTable(
                name: "SapFields");

            migrationBuilder.DropTable(
                name: "SapTables");

            migrationBuilder.DropTable(
                name: "SapReferenceSources");
        }
    }
}
