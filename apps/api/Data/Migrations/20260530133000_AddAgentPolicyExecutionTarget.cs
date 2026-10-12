using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPilot.Api.Data.Migrations
{
    [DbContext(typeof(JobPilotDbContext))]
    [Migration("20260530133000_AddAgentPolicyExecutionTarget")]
    public partial class AddAgentPolicyExecutionTarget : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionTarget",
                table: "AgentPolicies",
                type: "text",
                nullable: false,
                defaultValue: "extension");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionTarget",
                table: "AgentPolicies");
        }
    }
}
