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
            // Resume library, targets, evidence maps and ApplicationJobs columns/indices
            // belong to the four preceding manual migrations. Keep their ownership intact.
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

            migrationBuilder.CreateIndex(
                name: "IX_ResumeReviewAnswers_UserId_JobRequirementId",
                table: "ResumeReviewAnswers",
                columns: new[] { "UserId", "JobRequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeReviewAnswers_UserId_ResumeEvidenceMapRunId",
                table: "ResumeReviewAnswers",
                columns: new[] { "UserId", "ResumeEvidenceMapRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ResumeReviewAnswers");
        }
    }
}
