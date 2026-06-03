using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260602095500_AddResumeEvidenceMaps")]
    public partial class AddResumeEvidenceMaps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        name: "FK_ResumeRequirementEvidences_ResumeEvidenceMapRuns_ResumeEvidenceMapRunId",
                        column: x => x.ResumeEvidenceMapRunId,
                        principalTable: "ResumeEvidenceMapRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_ResumeEvidenceMapRuns_UserId_ResumeVersionId_JobTargetId_CreatedAt", table: "ResumeEvidenceMapRuns", columns: new[] { "UserId", "ResumeVersionId", "JobTargetId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ResumeRequirementEvidences_ResumeEvidenceMapRunId_SortOrder", table: "ResumeRequirementEvidences", columns: new[] { "ResumeEvidenceMapRunId", "SortOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ResumeRequirementEvidences");
            migrationBuilder.DropTable(name: "ResumeEvidenceMapRuns");
        }
    }
}
