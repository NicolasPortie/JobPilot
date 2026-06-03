using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntelligentAgentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutoSubmitMode = table.Column<string>(type: "text", nullable: false),
                    MaxApplicationsPerRun = table.Column<int>(type: "integer", nullable: false),
                    MaxApplicationsPerDay = table.Column<int>(type: "integer", nullable: false),
                    AllowedDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    BlockedDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    PolicyMode = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    PageState = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    TargetLabel = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    EvidencePath = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    ApiFormat = table.Column<string>(type: "text", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    AnswerValue = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ProviderModel = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    ValueSource = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteWorkflowMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    ActionJson = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWorkflowMemories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPolicies_UserId",
                table: "AgentPolicies",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_UserId_Name",
                table: "AiProviders",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteWorkflowMemories_UserId_Domain_PatternKey",
                table: "SiteWorkflowMemories",
                columns: new[] { "UserId", "Domain", "PatternKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPolicies");

            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "AgentSteps");

            migrationBuilder.DropTable(
                name: "AiProviders");

            migrationBuilder.DropTable(
                name: "ApplicationAnswers");

            migrationBuilder.DropTable(
                name: "ApplicationFields");

            migrationBuilder.DropTable(
                name: "SiteWorkflowMemories");
        }
    }
}
