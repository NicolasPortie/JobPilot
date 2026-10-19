using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyAgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDevices");

            migrationBuilder.DropTable(
                name: "AgentLearnings");

            migrationBuilder.DropTable(
                name: "AgentPolicies");

            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "AgentSteps");

            migrationBuilder.DropTable(
                name: "AgentTraceSteps");

            migrationBuilder.DropTable(
                name: "ApplicationAnswers");

            migrationBuilder.DropTable(
                name: "ApplicationFields");

            migrationBuilder.DropTable(
                name: "ApplicationJobs");

            migrationBuilder.DropTable(
                name: "Checkpoints");

            migrationBuilder.DropTable(
                name: "ExecutionEvents");

            migrationBuilder.DropTable(
                name: "JobFilters");

            migrationBuilder.DropTable(
                name: "PlatformCredentials");

            migrationBuilder.DropTable(
                name: "QuestionPatterns");

            migrationBuilder.DropTable(
                name: "ReusableAnswers");

            migrationBuilder.DropTable(
                name: "SiteWorkflowMemories");

            migrationBuilder.DropTable(
                name: "ApplicationRuns");

            migrationBuilder.DropTable(
                name: "JobLeads");

            migrationBuilder.DropIndex(
                name: "IX_JobTargets_UserId_JobLeadId",
                table: "JobTargets");

            migrationBuilder.DropColumn(
                name: "JobLeadId",
                table: "ResumeVersions");

            migrationBuilder.DropColumn(
                name: "JobLeadId",
                table: "ResumeTailoringRuns");

            migrationBuilder.DropColumn(
                name: "JobLeadId",
                table: "JobTargets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JobLeadId",
                table: "ResumeVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobLeadId",
                table: "ResumeTailoringRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobLeadId",
                table: "JobTargets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentLearnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionJson = table.Column<string>(type: "text", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LearningType = table.Column<string>(type: "text", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TriggerJson = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLearnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    AutoSubmitMode = table.Column<string>(type: "text", nullable: false),
                    BlockedDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    ExecutionTarget = table.Column<string>(type: "text", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    MaxApplicationsPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxApplicationsPerRun = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    PolicyMode = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidencePath = table.Column<string>(type: "text", nullable: false),
                    PageState = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    TargetLabel = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentTraceSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterFingerprint = table.Column<string>(type: "text", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeFingerprint = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentSafetyJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidencePath = table.Column<string>(type: "text", nullable: false),
                    ExecutionJson = table.Column<string>(type: "text", nullable: false),
                    GuardVerdictJson = table.Column<string>(type: "text", nullable: false),
                    Module = table.Column<string>(type: "text", nullable: false),
                    ObjectiveJson = table.Column<string>(type: "text", nullable: false),
                    ObjectiveKind = table.Column<string>(type: "text", nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Planner = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    ProposalJson = table.Column<string>(type: "text", nullable: false),
                    RecoveryJson = table.Column<string>(type: "text", nullable: false),
                    RiskJson = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTraceSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerValue = table.Column<string>(type: "text", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderModel = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueSource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    TotalJobs = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    FieldLabel = table.Column<string>(type: "text", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    ResolutionStatus = table.Column<string>(type: "text", nullable: false),
                    ResolutionValue = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResumeMode = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    SuggestedUserAction = table.Column<string>(type: "text", nullable: false),
                    TraceStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedApplyTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedRoleKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedSeniorities = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedWorkplaceTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    AutoIgnoreLowScore = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedRoleKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    BlockedSeniorities = table.Column<string[]>(type: "text[]", nullable: false),
                    BlockedWorkplaceTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MinScore = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyPlatform = table.Column<string>(type: "text", nullable: false),
                    ApplySurface = table.Column<string>(type: "text", nullable: false),
                    ApplyUrl = table.Column<string>(type: "text", nullable: false),
                    AutomationReason = table.Column<string>(type: "text", nullable: false),
                    AutomationSupport = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FilteredReason = table.Column<string>(type: "text", nullable: false),
                    FoundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemote = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ScoreReason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLeads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerTemplate = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ConfidenceThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    ProfileFieldMapping = table.Column<string>(type: "text", nullable: false),
                    QuestionTemplates = table.Column<string[]>(type: "text[]", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueSource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReusableAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerValue = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuestionKey = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReusableAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteWorkflowMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionJson = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWorkflowMemories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobLeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeExportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResumeVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobUrl = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ResumeFilePath = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_ApplicationJobs_ResumeExports_ResumeExportId",
                        column: x => x.ResumeExportId,
                        principalTable: "ResumeExports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApplicationJobs_ResumeVersions_ResumeVersionId",
                        column: x => x.ResumeVersionId,
                        principalTable: "ResumeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobTargets_UserId_JobLeadId",
                table: "JobTargets",
                columns: new[] { "UserId", "JobLeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_UserId_Domain_PatternKey",
                table: "AgentLearnings",
                columns: new[] { "UserId", "Domain", "PatternKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPolicies_UserId",
                table: "AgentPolicies",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentTraceSteps_UserId_AgentRunId_StepNumber",
                table: "AgentTraceSteps",
                columns: new[] { "UserId", "AgentRunId", "StepNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTraceSteps_UserId_ApplicationJobId_StepNumber",
                table: "AgentTraceSteps",
                columns: new[] { "UserId", "ApplicationJobId", "StepNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ApplicationRunId",
                table: "ApplicationJobs",
                column: "ApplicationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_JobLeadId",
                table: "ApplicationJobs",
                column: "JobLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ResumeExportId",
                table: "ApplicationJobs",
                column: "ResumeExportId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ResumeVersionId",
                table: "ApplicationJobs",
                column: "ResumeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeExportId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeExportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeVersionId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_UserId_TraceStepId",
                table: "Checkpoints",
                columns: new[] { "UserId", "TraceStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobFilters_UserId",
                table: "JobFilters",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobLeads_UserId_Url",
                table: "JobLeads",
                columns: new[] { "UserId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPatterns_UserId_PatternKey",
                table: "QuestionPatterns",
                columns: new[] { "UserId", "PatternKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReusableAnswers_UserId_QuestionKey",
                table: "ReusableAnswers",
                columns: new[] { "UserId", "QuestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteWorkflowMemories_UserId_Domain_PatternKey",
                table: "SiteWorkflowMemories",
                columns: new[] { "UserId", "Domain", "PatternKey" },
                unique: true);
        }
    }
}
