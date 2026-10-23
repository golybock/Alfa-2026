using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AlfaNews.Core.Data.Migrations;

public partial class NewsImportanceAndSourceCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "poll_seconds",
            table: "source",
            type: "integer",
            nullable: false,
            defaultValue: 3600,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 120);

        migrationBuilder.AddColumn<bool>(
            name: "is_specialized",
            table: "source",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Vector>(
            name: "embedding",
            table: "news_raw",
            type: "vector(768)",
            nullable: true);

        migrationBuilder.AddColumn<JsonElement>(
            name: "filter_json",
            table: "news_raw",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "importance",
            table: "news_raw",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "news_pending_idx",
            table: "news_raw",
            columns: new[] { "status", "importance", "fetched_at" });

        migrationBuilder.AddCheckConstraint(
            name: "news_raw_importance_check",
            table: "news_raw",
            sql: "importance IS NULL OR importance BETWEEN 1 AND 3");
        using var stream = typeof(NewsImportanceAndSourceCatalog).Assembly.GetManifestResourceStream("NewsSourcesSeed")
            ?? throw new InvalidOperationException("Не найден каталог источников новостей.");
        using var json = JsonDocument.Parse(stream);
        var seed = json.RootElement.GetRawText().Replace("'", "''");
        migrationBuilder.Sql($"""
            INSERT INTO source (id, name, feed_url, kind, allowed_hosts, enabled, poll_seconds, reliability, validation_status, is_specialized)
            SELECT s->>'id', s->>'name', s->>'feed_url', s->>'kind', s->'allowed_hosts',
                (s->>'enabled')::boolean AND s->>'validation_status' = 'checked', (s->>'poll_seconds')::integer,
                (s->>'reliability')::numeric, s->>'validation_status', (s->>'is_specialized')::boolean
            FROM jsonb_array_elements('{seed}'::jsonb) s
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, feed_url = EXCLUDED.feed_url,
                kind = EXCLUDED.kind, allowed_hosts = EXCLUDED.allowed_hosts, enabled = EXCLUDED.enabled,
                poll_seconds = EXCLUDED.poll_seconds, reliability = EXCLUDED.reliability,
                validation_status = EXCLUDED.validation_status, is_specialized = EXCLUDED.is_specialized;
            UPDATE news_raw n SET importance = 1 FROM source s
            WHERE n.source_id = s.id AND s.is_specialized AND n.importance IS NULL;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "news_pending_idx",
            table: "news_raw");

        migrationBuilder.DropCheckConstraint(
            name: "news_raw_importance_check",
            table: "news_raw");

        migrationBuilder.DropColumn(
            name: "is_specialized",
            table: "source");

        migrationBuilder.DropColumn(
            name: "embedding",
            table: "news_raw");

        migrationBuilder.DropColumn(
            name: "filter_json",
            table: "news_raw");

        migrationBuilder.DropColumn(
            name: "importance",
            table: "news_raw");

        migrationBuilder.AlterColumn<int>(
            name: "poll_seconds",
            table: "source",
            type: "integer",
            nullable: false,
            defaultValue: 120,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3600);
    }
}