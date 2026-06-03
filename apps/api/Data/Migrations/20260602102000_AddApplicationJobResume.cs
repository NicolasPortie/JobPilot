using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260602102000_AddApplicationJobResume")]
    public partial class AddApplicationJobResume : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResumeVersionId",
                table: "ApplicationJobs",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeVersionId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_UserId_ResumeExportId",
                table: "ApplicationJobs",
                columns: new[] { "UserId", "ResumeExportId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ApplicationJobs_UserId_ResumeVersionId", table: "ApplicationJobs");
            migrationBuilder.DropIndex(name: "IX_ApplicationJobs_UserId_ResumeExportId", table: "ApplicationJobs");
            migrationBuilder.DropColumn(name: "ResumeVersionId", table: "ApplicationJobs");
            migrationBuilder.DropColumn(name: "ResumeExportId", table: "ApplicationJobs");
            migrationBuilder.DropColumn(name: "ResumeFilePath", table: "ApplicationJobs");
        }
    }
}
