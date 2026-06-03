using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260603110500_AddApplicationJobResumeForeignKeys")]
    public partial class AddApplicationJobResumeForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ApplicationJobs"
                SET "ResumeVersionId" = NULL
                WHERE "ResumeVersionId" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM "ResumeVersions"
                    WHERE "ResumeVersions"."Id" = "ApplicationJobs"."ResumeVersionId"
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE "ApplicationJobs"
                SET "ResumeExportId" = NULL, "ResumeFilePath" = ''
                WHERE "ResumeExportId" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM "ResumeExports"
                    WHERE "ResumeExports"."Id" = "ApplicationJobs"."ResumeExportId"
                  );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationJobs_ResumeVersions_ResumeVersionId",
                table: "ApplicationJobs",
                column: "ResumeVersionId",
                principalTable: "ResumeVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationJobs_ResumeExports_ResumeExportId",
                table: "ApplicationJobs",
                column: "ResumeExportId",
                principalTable: "ResumeExports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationJobs_ResumeVersions_ResumeVersionId",
                table: "ApplicationJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationJobs_ResumeExports_ResumeExportId",
                table: "ApplicationJobs");
        }
    }
}
