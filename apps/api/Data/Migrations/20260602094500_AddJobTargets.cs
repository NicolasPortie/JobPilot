using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260602094500_AddJobTargets")]
    public partial class AddJobTargets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(name: "IX_JobRequirements_JobTargetId_SortOrder", table: "JobRequirements", columns: new[] { "JobTargetId", "SortOrder" });
            migrationBuilder.CreateIndex(name: "IX_JobTargets_UserId_JobLeadId", table: "JobTargets", columns: new[] { "UserId", "JobLeadId" });
            migrationBuilder.CreateIndex(name: "IX_JobTargets_UserId_UpdatedAt", table: "JobTargets", columns: new[] { "UserId", "UpdatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "JobRequirements");
            migrationBuilder.DropTable(name: "JobTargets");
        }
    }
}
