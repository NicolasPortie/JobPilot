using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    TotalJobs = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    LinkedinUrl = table.Column<string>(type: "text", nullable: false),
                    GithubUrl = table.Column<string>(type: "text", nullable: false),
                    PortfolioUrl = table.Column<string>(type: "text", nullable: false),
                    ProfessionalSummary = table.Column<string>(type: "text", nullable: false),
                    Skills = table.Column<string[]>(type: "text[]", nullable: false),
                    TargetRole = table.Column<string>(type: "text", nullable: false),
                    Seniority = table.Column<string>(type: "text", nullable: false),
                    SalaryExpectation = table.Column<string>(type: "text", nullable: false),
                    ResumePath = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    FieldLabel = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    ResolutionStatus = table.Column<string>(type: "text", nullable: false),
                    ResolutionValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ScoreReason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsRemote = table.Column<bool>(type: "boolean", nullable: false),
                    FoundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLeads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReusableAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionKey = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    AnswerValue = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReusableAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Keywords = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Workplace = table.Column<string>(type: "text", nullable: false),
                    Seniority = table.Column<string>(type: "text", nullable: false),
                    PostedWithinDays = table.Column<int>(type: "integer", nullable: false),
                    LimitPerRun = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobLeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobUrl = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationJobs_ApplicationRuns_ApplicationRunId",
                        column: x => x.ApplicationRunId,
                        principalTable: "ApplicationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationJobs_JobLeads_JobLeadId",
                        column: x => x.JobLeadId,
                        principalTable: "JobLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ApplicationRunId",
                table: "ApplicationJobs",
                column: "ApplicationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_JobLeadId",
                table: "ApplicationJobs",
                column: "JobLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_UserId",
                table: "CandidateProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobLeads_UserId_Url",
                table: "JobLeads",
                columns: new[] { "UserId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReusableAnswers_UserId_QuestionKey",
                table: "ReusableAnswers",
                columns: new[] { "UserId", "QuestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDevices");

            migrationBuilder.DropTable(
                name: "ApplicationJobs");

            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropTable(
                name: "Checkpoints");

            migrationBuilder.DropTable(
                name: "ExecutionEvents");

            migrationBuilder.DropTable(
                name: "ReusableAnswers");

            migrationBuilder.DropTable(
                name: "SavedSearches");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ApplicationRuns");

            migrationBuilder.DropTable(
                name: "JobLeads");
        }
    }
}
