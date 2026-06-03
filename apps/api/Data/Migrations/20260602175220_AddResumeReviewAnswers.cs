using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeReviewAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResumeExportId",
                table: "ApplicationJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeFilePath",
                table: "ApplicationJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ResumeVersionId",
                table: "ApplicationJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKind = table.Column<string>(type: "text", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Seniority = table.Column<string>(type: "text", nullable: false),
                    Workplace = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeImportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SourceFileName = table.Column<string>(type: "text", nullable: false),
                    SourceKind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeEvidenceMapRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EvidenceScore = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeEvidenceMapRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    ParseText = table.Column<string>(type: "text", nullable: false),
                    ParseWarningsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileName = table.Column<string>(type: "text", nullable: false),
                    SourcePath = table.Column<string>(type: "text", nullable: false),
                    SourceKind = table.Column<string>(type: "text", nullable: false),
                    ExtractionMethod = table.Column<string>(type: "text", nullable: false),
                    Classification = table.Column<string>(type: "text", nullable: false),
                    TextQualityScore = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiresOcr = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    Pages = table.Column<int>(type: "integer", nullable: false),
                    PagesWithNativeText = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeReviewAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeEvidenceMapRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementLabel = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    AnswerValue = table.Column<string>(type: "text", nullable: false),
                    AnswerType = table.Column<string>(type: "text", nullable: false),
                    EvidenceText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeReviewAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeTailoringRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderModel = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeTailoringRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Importance = table.Column<string>(type: "text", nullable: false),
                    EvidenceText = table.Column<string>(type: "text", nullable: false),
                    NormalizedKeyword = table.Column<string>(type: "text", nullable: false),
                    ExactPhrases = table.Column<string[]>(type: "text[]", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequirements_JobTargets_JobTargetId",
                        column: x => x.JobTargetId,
                        principalTable: "JobTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TargetCompany = table.Column<string>(type: "text", nullable: false),
                    TargetRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AtsScore = table.Column<int>(type: "integer", nullable: false),
                    AtsReportJson = table.Column<string>(type: "text", nullable: false),
                    PlainText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeVersions_ResumeDocuments_ResumeDocumentId",
                        column: x => x.ResumeDocumentId,
                        principalTable: "ResumeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeRequirementEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeEvidenceMapRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementLabel = table.Column<string>(type: "text", nullable: false),
                    RequirementCategory = table.Column<string>(type: "text", nullable: false),
                    RequirementImportance = table.Column<string>(type: "text", nullable: false),
                    MatchStatus = table.Column<string>(type: "text", nullable: false),
                    EvidenceText = table.Column<string>(type: "text", nullable: false),
                    EvidenceSource = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeRequirementEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeRequirementEvidences_ResumeEvidenceMapRuns_ResumeEvid~",
                        column: x => x.ResumeEvidenceMapRunId,
                        principalTable: "ResumeEvidenceMapRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeSections_ResumeVersions_ResumeVersionId",
                        column: x => x.ResumeVersionId,
                        principalTable: "ResumeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeExportId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeExportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeVersionId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequirements_JobTargetId_SortOrder",
                table: "JobRequirements",
                columns: new[] { "JobTargetId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_JobTargets_UserId_JobLeadId",
                table: "JobTargets",
                columns: new[] { "UserId", "JobLeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobTargets_UserId_UpdatedAt",
                table: "JobTargets",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeDocuments_UserId_UpdatedAt",
                table: "ResumeDocuments",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeEvidenceMapRuns_UserId_ResumeVersionId_JobTargetId_Cr~",
                table: "ResumeEvidenceMapRuns",
                columns: new[] { "UserId", "ResumeVersionId", "JobTargetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeExports_UserId_ResumeVersionId_CreatedAt",
                table: "ResumeExports",
                columns: new[] { "UserId", "ResumeVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeImports_UserId_CreatedAt",
                table: "ResumeImports",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeRequirementEvidences_ResumeEvidenceMapRunId_SortOrder",
                table: "ResumeRequirementEvidences",
                columns: new[] { "ResumeEvidenceMapRunId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeReviewAnswers_UserId_JobRequirementId",
                table: "ResumeReviewAnswers",
                columns: new[] { "UserId", "JobRequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeReviewAnswers_UserId_ResumeEvidenceMapRunId",
                table: "ResumeReviewAnswers",
                columns: new[] { "UserId", "ResumeEvidenceMapRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeSections_ResumeVersionId_SortOrder",
                table: "ResumeSections",
                columns: new[] { "ResumeVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeTailoringRuns_UserId_ResumeVersionId_CreatedAt",
                table: "ResumeTailoringRuns",
                columns: new[] { "UserId", "ResumeVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeVersions_ResumeDocumentId",
                table: "ResumeVersions",
                column: "ResumeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeVersions_UserId_ResumeDocumentId_UpdatedAt",
                table: "ResumeVersions",
                columns: new[] { "UserId", "ResumeDocumentId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRequirements");

            migrationBuilder.DropTable(
                name: "ResumeExports");

            migrationBuilder.DropTable(
                name: "ResumeImports");

            migrationBuilder.DropTable(
                name: "ResumeRequirementEvidences");

            migrationBuilder.DropTable(
                name: "ResumeReviewAnswers");

            migrationBuilder.DropTable(
                name: "ResumeSections");

            migrationBuilder.DropTable(
                name: "ResumeTailoringRuns");

            migrationBuilder.DropTable(
                name: "JobTargets");

            migrationBuilder.DropTable(
                name: "ResumeEvidenceMapRuns");

            migrationBuilder.DropTable(
                name: "ResumeVersions");

            migrationBuilder.DropTable(
                name: "ResumeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationJobs_UserId_ResumeExportId",
                table: "ApplicationJobs");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationJobs_UserId_ResumeVersionId",
                table: "ApplicationJobs");

            migrationBuilder.DropColumn(
                name: "ResumeExportId",
                table: "ApplicationJobs");

            migrationBuilder.DropColumn(
                name: "ResumeFilePath",
                table: "ApplicationJobs");

            migrationBuilder.DropColumn(
                name: "ResumeVersionId",
                table: "ApplicationJobs");
        }
    }
}
