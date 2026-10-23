using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaNews.Core.Data.Migrations;

public partial class AuditDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "finish_reason",
            table: "llm_usage",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "http_status",
            table: "llm_usage",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<JsonElement>(
            name: "arguments_json",
            table: "agent_step",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<JsonElement>(
            name: "result_json",
            table: "agent_step",
            type: "jsonb",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "finish_reason",
            table: "llm_usage");

        migrationBuilder.DropColumn(
            name: "http_status",
            table: "llm_usage");

        migrationBuilder.DropColumn(
            name: "arguments_json",
            table: "agent_step");

        migrationBuilder.DropColumn(
            name: "result_json",
            table: "agent_step");
    }
}