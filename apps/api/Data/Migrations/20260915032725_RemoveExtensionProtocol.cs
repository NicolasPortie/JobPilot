using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExtensionProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserExtensionCommandJobs");

            migrationBuilder.DropTable(
                name: "BrowserExtensionSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrowserExtensionCommandJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrowserExtensionSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ElementId = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserExtensionCommandJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrowserExtensionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Browser = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentUrl = table.Column<string>(type: "text", nullable: false),
                    ExtensionVersion = table.Column<string>(type: "text", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    IsSupportedPage = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserExtensionSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserExtensionCommandJobs_BrowserExtensionSessionId_Statu~",
                table: "BrowserExtensionCommandJobs",
                columns: new[] { "BrowserExtensionSessionId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserExtensionSessions_TokenHash",
                table: "BrowserExtensionSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrowserExtensionSessions_UserId_CreatedAt",
                table: "BrowserExtensionSessions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserExtensionSessions_UserId_InstallationId",
                table: "BrowserExtensionSessions",
                columns: new[] { "UserId", "InstallationId" });
        }
    }
}
