using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AlfaNews.Core.Data.Migrations;

[DbContext(typeof(AlfaNewsDbContext))]
[Migration("20261022201000_SeedBankResearchOcr")]
public sealed class SeedBankResearchOcr : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(SeedBankResearchOcr).Assembly.GetManifestResourceStream("BankResearchOcrSeed")
            ?? throw new InvalidOperationException("Не найден комплект распознанных документов.");
        using var json = JsonDocument.Parse(stream);
        var seed = json.RootElement.GetRawText().Replace("'", "''");
        migrationBuilder.Sql($"""
            CREATE TEMP TABLE a08_seed (body jsonb) ON COMMIT DROP;
            INSERT INTO a08_seed VALUES ('{seed}'::jsonb);
            CREATE TEMP TABLE a08_documents (id uuid PRIMARY KEY) ON COMMIT DROP;
            WITH inserted AS (
                INSERT INTO research_doc (id, sha256, file_path, title, source_url, published_on, page_count,
                    ocr_status, review_status, version, created_at)
                SELECT (d->>'id')::uuid, d->>'sha256', d->>'file_path', d->>'title', d->>'source_url',
                    (d->>'published_on')::date, (d->>'page_count')::integer, d->>'ocr_status', 'not_reviewed',
                    d->>'version', (d->>'created_at')::timestamptz
                FROM a08_seed CROSS JOIN LATERAL jsonb_array_elements(body->'documents') d
                ON CONFLICT (sha256) DO NOTHING RETURNING id
            ) INSERT INTO a08_documents SELECT id FROM inserted;
            INSERT INTO research_page (id, document_id, page_no, image_path, ocr_status, ocr_json, review_status, last_error)
            SELECT (p->>'id')::uuid, (p->>'document_id')::uuid, (p->>'page_no')::integer, p->>'image_path',
                p->>'ocr_status', NULLIF(p->'ocr_json', 'null'::jsonb), 'not_reviewed', p->>'last_error'
            FROM a08_seed CROSS JOIN LATERAL jsonb_array_elements(body->'pages') p
            JOIN a08_documents d ON d.id = (p->>'document_id')::uuid;
            INSERT INTO research_chunk (id, page_id, local_id, kind, text, tables_json, chart_data, tags_json,
                review_status, review_note, version, search_text)
            SELECT (c->>'id')::uuid, (c->>'page_id')::uuid, c->>'local_id', c->>'kind', c->>'text',
                c->'tables_json', c->'chart_data', c->'tags_json', 'not_reviewed', NULL, c->>'version',
                to_tsvector('russian', c->>'text')
            FROM a08_seed CROSS JOIN LATERAL jsonb_array_elements(body->'chunks') c
            JOIN research_page p ON p.id = (c->>'page_id')::uuid
            JOIN a08_documents d ON d.id = p.document_id;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}