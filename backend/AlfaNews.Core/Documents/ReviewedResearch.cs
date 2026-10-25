using System.Text.RegularExpressions;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Documents;

// Обзоры банка, отмеченные проверенными в data/bank_research/index.json, считаются проверенными вручную: документ, его
// распознанные страницы, блоки и графики. Отметку ставят миграция MarkBankResearchReviewed (стартовый комплект в базе из сида)
// и каждый обход PDF, а страницы, распознанные позже, наследуют её от документа (PdfProcessing). Содержимое и версии не меняются.
public static partial class ReviewedResearch
{
    public static async Task<int> ApplyAsync(AlfaNewsDbContext db, IReadOnlyCollection<string> sha256,
        CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var statement in Statements(sha256))
            changed += await db.Database.ExecuteSqlRawAsync(statement.Replace("{", "{{").Replace("}", "}}"),
                cancellationToken);
        return changed;
    }

    public static IReadOnlyList<string> Statements(IReadOnlyCollection<string> sha256)
    {
        if (sha256.Count == 0)
            return [];
        if (sha256.Any(hash => !Sha256Pattern().IsMatch(hash)))
            throw new ArgumentException("Ожидаются SHA-256 в нижнем регистре.", nameof(sha256));
        var hashes = "ARRAY[" + string.Join(",", sha256.Select(hash => $"'{hash}'")) + "]";
        return
        [
            $"UPDATE research_doc SET review_status = 'reviewed' WHERE sha256 = ANY({hashes}) AND review_status <> 'reviewed'",
            $"""
             UPDATE research_page AS page SET review_status = 'reviewed'
             FROM research_doc AS doc
             WHERE page.document_id = doc.id AND doc.sha256 = ANY({hashes}) AND page.ocr_status = 'ready' AND page.review_status <> 'reviewed'
             """,
            $$"""
              UPDATE research_chunk AS chunk SET review_status = 'reviewed',
                  chart_data = COALESCE((SELECT jsonb_agg(jsonb_set(chart, '{review_status}', '"reviewed"')) FROM jsonb_array_elements(chunk.chart_data) AS chart), '[]'::jsonb)
              FROM research_page AS page JOIN research_doc AS doc ON doc.id = page.document_id
              WHERE chunk.page_id = page.id AND doc.sha256 = ANY({{hashes}})
                  AND (chunk.review_status <> 'reviewed' OR chunk.chart_data::text LIKE '%"review_status": "not_reviewed"%'
                      OR chunk.chart_data::text LIKE '%"review_status": "partially_reviewed"%')
              """
        ];
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();
}