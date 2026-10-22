using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaNews.Core.Data.Migrations;

public partial class LlmAttemptReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "request_key",
            table: "llm_usage",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<decimal>(
            name: "reserved_cost_rub",
            table: "llm_usage",
            type: "numeric(18,8)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateIndex(
            name: "usage_request_stage_idx",
            table: "llm_usage",
            columns: new[] { "request_key", "stage" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "usage_request_stage_idx",
            table: "llm_usage");

        migrationBuilder.DropColumn(
            name: "request_key",
            table: "llm_usage");

        migrationBuilder.DropColumn(
            name: "reserved_cost_rub",
            table: "llm_usage");
    }
}