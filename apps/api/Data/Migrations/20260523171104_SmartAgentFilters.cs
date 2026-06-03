using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SmartAgentFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilteredReason",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "JobFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedSeniorities = table.Column<string[]>(type: "text[]", nullable: false),
                    BlockedSeniorities = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedWorkplaceTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    BlockedWorkplaceTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedApplyTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    MinScore = table.Column<int>(type: "integer", nullable: false),
                    AutoIgnoreLowScore = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfileValidationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    ValidationType = table.Column<string>(type: "text", nullable: false),
                    ValidationValue = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileValidationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    QuestionTemplates = table.Column<string[]>(type: "text[]", nullable: false),
                    AnswerTemplate = table.Column<string>(type: "text", nullable: false),
                    ValueSource = table.Column<string>(type: "text", nullable: false),
                    ProfileFieldMapping = table.Column<string>(type: "text", nullable: false),
                    ConfidenceThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionPatterns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobFilters_UserId",
                table: "JobFilters",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileValidationRules_UserId_FieldName",
                table: "ProfileValidationRules",
                columns: new[] { "UserId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPatterns_UserId_PatternKey",
                table: "QuestionPatterns",
                columns: new[] { "UserId", "PatternKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobFilters");

            migrationBuilder.DropTable(
                name: "ProfileValidationRules");

            migrationBuilder.DropTable(
                name: "QuestionPatterns");

            migrationBuilder.DropColumn(
                name: "FilteredReason",
                table: "JobLeads");
        }
    }
}
