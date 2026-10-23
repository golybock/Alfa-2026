using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AlfaNews.Core.Data.Migrations;

[DbContext(typeof(AlfaNewsDbContext))]
[Migration("20261023130000_RefreshBankResearchOcr")]
public sealed class RefreshBankResearchOcr : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(RefreshBankResearchOcr).Assembly.GetManifestResourceStream("BankResearchOcrSeed")
            ?? throw new InvalidOperationException("Не найден комплект распознанных документов.");
        using var json = JsonDocument.Parse(stream);
        var seed = json.RootElement.GetRawText().Replace("'", "''");
        migrationBuilder.Sql($"""
            CREATE TEMP TABLE a08_refresh (body jsonb) ON COMMIT DROP;
            INSERT INTO a08_refresh VALUES ('{seed}'::jsonb);
            UPDATE research_doc d
            SET published_on = (sd->>'published_on')::date, ocr_status = sd->>'ocr_status', version = sd->>'version'
            FROM a08_refresh r CROSS JOIN LATERAL jsonb_array_elements(r.body->'documents') sd
            WHERE d.sha256 = sd->>'sha256' AND d.review_status = 'not_reviewed';
            CREATE TEMP TABLE a08_refresh_pages ON COMMIT DROP AS
            SELECT p.id AS target_id, (sp->>'id')::uuid AS seed_id, sp
            FROM a08_refresh r
            CROSS JOIN LATERAL jsonb_array_elements(r.body->'pages') sp
            CROSS JOIN LATERAL jsonb_array_elements(r.body->'documents') sd
            JOIN research_doc d ON d.sha256 = sd->>'sha256'
            JOIN research_page p ON p.document_id = d.id AND p.page_no = (sp->>'page_no')::integer
            WHERE sd->>'id' = sp->>'document_id' AND p.review_status = 'not_reviewed';
            UPDATE research_page p
            SET ocr_status = rp.sp->>'ocr_status', ocr_json = NULLIF(rp.sp->'ocr_json', 'null'::jsonb), last_error = rp.sp->>'last_error'
            FROM a08_refresh_pages rp
            WHERE p.id = rp.target_id;
            DELETE FROM research_chunk c USING a08_refresh_pages rp
            WHERE c.page_id = rp.target_id
                AND c.id NOT IN (SELECT (s->>'id')::uuid FROM a08_refresh r CROSS JOIN LATERAL jsonb_array_elements(r.body->'chunks') s);
            INSERT INTO research_chunk (id, page_id, local_id, kind, text, tables_json, chart_data, tags_json,
                review_status, review_note, version, search_text)
            SELECT (c->>'id')::uuid, rp.target_id, c->>'local_id', c->>'kind', c->>'text',
                c->'tables_json', c->'chart_data', c->'tags_json', 'not_reviewed', NULL, c->>'version',
                to_tsvector('russian', c->>'text')
            FROM a08_refresh r
            CROSS JOIN LATERAL jsonb_array_elements(r.body->'chunks') c
            JOIN a08_refresh_pages rp ON rp.seed_id = (c->>'page_id')::uuid
            ON CONFLICT (id) DO UPDATE SET page_id = EXCLUDED.page_id, local_id = EXCLUDED.local_id, kind = EXCLUDED.kind,
                text = EXCLUDED.text, tables_json = EXCLUDED.tables_json, chart_data = EXCLUDED.chart_data,
                tags_json = EXCLUDED.tags_json, version = EXCLUDED.version, search_text = EXCLUDED.search_text,
                embedding = CASE WHEN research_chunk.version = EXCLUDED.version AND research_chunk.text = EXCLUDED.text
                    THEN research_chunk.embedding END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}