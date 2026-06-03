using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLearnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentLearnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    LearningType = table.Column<string>(type: "text", nullable: false),
                    PatternKey = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TriggerJson = table.Column<string>(type: "text", nullable: false),
                    ActionJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLearnings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_UserId_Domain_PatternKey",
                table: "AgentLearnings",
                columns: new[] { "UserId", "Domain", "PatternKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLearnings");
        }
    }
}
