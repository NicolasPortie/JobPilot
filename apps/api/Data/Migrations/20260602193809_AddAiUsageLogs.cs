using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    ApiFormat = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    UsageJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_UserId_CreatedAt",
                table: "AiUsageLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_UserId_Operation_CreatedAt",
                table: "AiUsageLogs",
                columns: new[] { "UserId", "Operation", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLogs");
        }
    }
}
