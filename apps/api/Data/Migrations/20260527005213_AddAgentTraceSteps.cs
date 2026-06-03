using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTraceSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentRunId",
                table: "Checkpoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeMode",
                table: "Checkpoints",
                type: "text",
                nullable: false,
                defaultValue: "reobserve");

            migrationBuilder.AddColumn<string>(
                name: "SuggestedUserAction",
                table: "Checkpoints",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TraceStepId",
                table: "Checkpoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentTraceSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    ObjectiveKind = table.Column<string>(type: "text", nullable: false),
                    ObjectiveJson = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Module = table.Column<string>(type: "text", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    BeforeFingerprint = table.Column<string>(type: "text", nullable: false),
                    AfterFingerprint = table.Column<string>(type: "text", nullable: false),
                    Planner = table.Column<string>(type: "text", nullable: false),
                    ProposalJson = table.Column<string>(type: "text", nullable: false),
                    RiskJson = table.Column<string>(type: "text", nullable: false),
                    GuardVerdictJson = table.Column<string>(type: "text", nullable: false),
                    ExecutionJson = table.Column<string>(type: "text", nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    RecoveryJson = table.Column<string>(type: "text", nullable: false),
                    ContentSafetyJson = table.Column<string>(type: "text", nullable: false),
                    EvidencePath = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTraceSteps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_UserId_TraceStepId",
                table: "Checkpoints",
                columns: new[] { "UserId", "TraceStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTraceSteps_UserId_AgentRunId_StepNumber",
                table: "AgentTraceSteps",
                columns: new[] { "UserId", "AgentRunId", "StepNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTraceSteps_UserId_ApplicationJobId_StepNumber",
                table: "AgentTraceSteps",
                columns: new[] { "UserId", "ApplicationJobId", "StepNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentTraceSteps");

            migrationBuilder.DropIndex(
                name: "IX_Checkpoints_UserId_TraceStepId",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "AgentRunId",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "ResumeMode",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "SuggestedUserAction",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "TraceStepId",
                table: "Checkpoints");
        }
    }
}
