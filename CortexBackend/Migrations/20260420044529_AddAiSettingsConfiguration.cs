using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSettingsConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSettingsAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    ChangedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BeforeSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettingsAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSettingsAuditEntries_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSettingsConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsIntakeAssistEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsTriageEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsScreenshotInsightEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsSuggestedUpdatesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsPriorityRecommendationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsStatusRecommendationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefaultTextModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultVisionModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxTokens = table.Column<int>(type: "int", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    AdvisoryOnlyMode = table.Column<bool>(type: "bit", nullable: false),
                    AllowStatusRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    AllowPriorityRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    SuggestionOnlyMode = table.Column<bool>(type: "bit", nullable: false),
                    ConfidenceThreshold = table.Column<double>(type: "float", nullable: false),
                    MaxScreenshotAttachmentCount = table.Column<int>(type: "int", nullable: false),
                    LastModifiedBy = table.Column<int>(type: "int", nullable: true),
                    LastModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettingsConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSettingsConfigurations_Users_LastModifiedBy",
                        column: x => x.LastModifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSettingsAuditEntries_ChangedBy",
                table: "AiSettingsAuditEntries",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AiSettingsAuditEntries_ChangedDateUtc",
                table: "AiSettingsAuditEntries",
                column: "ChangedDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiSettingsConfigurations_LastModifiedBy",
                table: "AiSettingsConfigurations",
                column: "LastModifiedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSettingsAuditEntries");

            migrationBuilder.DropTable(
                name: "AiSettingsConfigurations");
        }
    }
}
