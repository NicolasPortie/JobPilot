using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DevSyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ResumeExportId",
                table: "ApplicationJobs",
                column: "ResumeExportId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationJobs_ResumeVersionId",
                table: "ApplicationJobs",
                column: "ResumeVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationJobs_ResumeExportId",
                table: "ApplicationJobs");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationJobs_ResumeVersionId",
                table: "ApplicationJobs");
        }
    }
}
