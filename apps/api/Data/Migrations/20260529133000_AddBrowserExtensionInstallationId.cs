using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260529133000_AddBrowserExtensionInstallationId")]
    public partial class AddBrowserExtensionInstallationId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstallationId",
                table: "BrowserExtensionSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserExtensionSessions_UserId_InstallationId",
                table: "BrowserExtensionSessions",
                columns: new[] { "UserId", "InstallationId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BrowserExtensionSessions_UserId_InstallationId",
                table: "BrowserExtensionSessions");

            migrationBuilder.DropColumn(
                name: "InstallationId",
                table: "BrowserExtensionSessions");
        }
    }
}
