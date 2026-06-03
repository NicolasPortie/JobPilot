using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260602093000_AddResumeLibrary")]
    public partial class AddResumeLibrary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(name: "IX_ResumeDocuments_UserId_UpdatedAt", table: "ResumeDocuments", columns: new[] { "UserId", "UpdatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ResumeExports_UserId_ResumeVersionId_CreatedAt", table: "ResumeExports", columns: new[] { "UserId", "ResumeVersionId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ResumeImports_UserId_CreatedAt", table: "ResumeImports", columns: new[] { "UserId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ResumeSections_ResumeVersionId_SortOrder", table: "ResumeSections", columns: new[] { "ResumeVersionId", "SortOrder" });
            migrationBuilder.CreateIndex(name: "IX_ResumeTailoringRuns_UserId_ResumeVersionId_CreatedAt", table: "ResumeTailoringRuns", columns: new[] { "UserId", "ResumeVersionId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ResumeVersions_ResumeDocumentId", table: "ResumeVersions", column: "ResumeDocumentId");
            migrationBuilder.CreateIndex(name: "IX_ResumeVersions_UserId_ResumeDocumentId_UpdatedAt", table: "ResumeVersions", columns: new[] { "UserId", "ResumeDocumentId", "UpdatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ResumeExports");
            migrationBuilder.DropTable(name: "ResumeSections");
            migrationBuilder.DropTable(name: "ResumeTailoringRuns");
            migrationBuilder.DropTable(name: "ResumeImports");
            migrationBuilder.DropTable(name: "ResumeVersions");
            migrationBuilder.DropTable(name: "ResumeDocuments");
        }
    }
}
