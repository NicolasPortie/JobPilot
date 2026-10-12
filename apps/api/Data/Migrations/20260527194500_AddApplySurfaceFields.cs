using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260527194500_AddApplySurfaceFields")]
    public partial class AddApplySurfaceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplyPlatform",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "ApplySurface",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "ApplyUrl",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AutomationReason",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AutomationSupport",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyPlatform",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "ApplySurface",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "ApplyUrl",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AutomationReason",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AutomationSupport",
                table: "JobLeads");
        }
    }
}
