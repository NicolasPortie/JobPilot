using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobFilterRoleKeywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "AllowedRoleKeywords",
                table: "JobFilters",
                type: "text[]",
                nullable: false,
                defaultValue: new[] { "asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect" });

            migrationBuilder.AddColumn<string[]>(
                name: "BlockedRoleKeywords",
                table: "JobFilters",
                type: "text[]",
                nullable: false,
                defaultValue: new[] { "suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados" });

            migrationBuilder.Sql("""
                UPDATE "JobFilters"
                SET "BlockedSeniorities" = array_remove(array_remove("BlockedSeniorities", 'architect'), 'arquiteto')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedRoleKeywords",
                table: "JobFilters");

            migrationBuilder.DropColumn(
                name: "BlockedRoleKeywords",
                table: "JobFilters");
        }
    }
}
