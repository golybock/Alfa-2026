using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Documents;

public sealed record ReviewResult(ReviewAcceptedResponse Response, IReadOnlyList<Guid> ChangedChunkIds);

public sealed class ResearchReview(AlfaNewsDbContext db, JobQueue queue, TimeProvider clock)
{
    private const int MaxTextLength = 20000;

    public async Task<ReviewResult> ApplyAsync(UserContext reviewer, Guid documentId, int pageNo, string? ifMatch,
        PageReviewRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM research_doc WHERE id = {documentId} FOR UPDATE",
            cancellationToken);
        var document = await db.ResearchDocRows.SingleOrDefaultAsync(row => row.Id == documentId, cancellationToken)
                       ?? throw new AccessException(404, "not_found", "Документ не найден.");
        var page = await db.ResearchPageRows.SingleOrDefaultAsync(
                       row => row.DocumentId == documentId && row.PageNo == pageNo, cancellationToken)
                   ?? throw new AccessException(404, "not_found", "Страница не найдена.");
        if (ifMatch is null || ifMatch.Trim().TrimStart('W', '/').Trim('"') != document.Version)
            throw new AccessException(409, "stale_review", "Документ изменился. Обновите страницу и повторите сверку.");
        if (page.OcrStatus != "ready")
            throw new AccessException(409, "invalid_input", "Страница ещё не распознана.");
        if (request.Chunks is not { Count: > 0 } ||
            request.Chunks.Select(item => item.ChunkId).Distinct(StringComparer.Ordinal).Count() !=
            request.Chunks.Count)
            throw InvalidInput();
        var chunks = await db.ResearchChunkRows.Where(row => row.PageId == page.Id).ToListAsync(cancellationToken);
        var changed = new List<Guid>();
        foreach (var review in request.Chunks)
        {
            var chunk = chunks.SingleOrDefault(row => row.Id.ToString() == review.ChunkId) ?? throw InvalidInput();
            if (string.IsNullOrWhiteSpace(review.CorrectedText) || review.CorrectedText.Length > MaxTextLength
                                                                || review.Note is { Length: > 2000 } ||
                                                                review.CorrectedTables is null || review.Charts is null
                                                                || review.CorrectedTables.Any(table =>
                                                                    table.Columns.Count == 0 || table.Rows.Any(row =>
                                                                        row.Count != table.Columns.Count)))
                throw InvalidInput();
            var charts = chunk.ChartData.Deserialize<List<ReviewableChartDto>>(JobKinds.JsonOptions) ?? [];
            if (review.Charts.Select(item => item.CorrectedChart.LocalId).Distinct(StringComparer.Ordinal).Count() !=
                review.Charts.Count)
                throw InvalidInput();
            foreach (var chart in review.Charts)
            {
                var index = charts.FindIndex(item => item.Chart.LocalId == chart.CorrectedChart.LocalId);
                var region = chart.CorrectedChart.Region;
                if (index < 0 || region.Count != 4 || region.Any(item => item is < 0 or > 1) ||
                    region[0] + region[2] > 1 || region[1] + region[3] > 1)
                    throw InvalidInput();
                charts[index] = new ReviewableChartDto(chart.CorrectedChart, chart.ReviewStatus);
            }

            if (review.ReviewStatus == ReviewStatus.Reviewed &&
                charts.Any(item => item.ReviewStatus != ReviewStatus.Reviewed))
                throw new AccessException(400, "invalid_input",
                    "Нельзя отметить блок проверенным, пока в нём есть непроверенный график.");
            var tables = JsonSerializer.SerializeToElement(review.CorrectedTables, JobKinds.JsonOptions);
            var chartData = JsonSerializer.SerializeToElement(charts, JobKinds.JsonOptions);
            // Новая версия нужна только при изменении содержимого: от неё зависят вектор и доказательства карточек.
            var before = Content(chunk.Text, chunk.TablesJson, chunk.ChartData);
            var after = Content(review.CorrectedText, tables, chartData);
            if (before != after)
            {
                chunk.Text = review.CorrectedText;
                chunk.TablesJson = tables;
                chunk.Embedding = null;
                chunk.Version = LlmPromptCatalog.Hash(chunk.Version + ":review:" + after);
                changed.Add(chunk.Id);
            }

            chunk.ChartData = chartData;
            chunk.ReviewStatus = Status(review.ReviewStatus);
            chunk.ReviewNote = string.IsNullOrWhiteSpace(review.Note) ? null : review.Note.Trim();
        }

        page.ReviewStatus = Aggregate(chunks.Select(row => row.ReviewStatus).ToArray());
        page.ReviewedBy = reviewer.UserId;
        page.ReviewedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        if (changed.Count > 0)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE research_chunk SET search_text = to_tsvector('russian', text) WHERE id = ANY({changed.ToArray()})",
                cancellationToken);
        var pages = await db.ResearchPageRows.Where(row => row.DocumentId == documentId).Select(row => row.ReviewStatus)
            .ToArrayAsync(cancellationToken);
        document.ReviewStatus = pages.Length == document.PageCount
            ? Aggregate(pages)
            : pages.Any(status => status != "not_reviewed")
                ? "partially_reviewed"
                : "not_reviewed";
        document.Version = LlmPromptCatalog.Hash(document.Version + ":review:" + page.Id + ":" +
                                                 page.ReviewedAt.Value.ToUnixTimeMilliseconds());
        await db.SaveChangesAsync(cancellationToken);
        foreach (var chunk in chunks.Where(row => changed.Contains(row.Id)))
            await queue.EnqueueAsync(new EmbedContentJob("research_chunk", chunk.Id.ToString(), chunk.Version),
                reviewer.UserId, null, cancellationToken);
        var job = await queue.EnqueueAsync(new FinalizePdfJob(document.Id.ToString()), reviewer.UserId, null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReviewResult(new ReviewAcceptedResponse(document.Id.ToString(), document.Version, job.Id.ToString()),
            changed);
    }

    private static string Content(string text, JsonElement tables, JsonElement charts) =>
        JsonSerializer.Serialize(new
        {
            text,
            tables = tables.GetRawText(),
            charts = (charts.Deserialize<List<ReviewableChartDto>>(JobKinds.JsonOptions) ?? []).Select(item =>
                item.Chart)
        }, JobKinds.JsonOptions);

    private static string Aggregate(IReadOnlyCollection<string> statuses) =>
        statuses.Count > 0 && statuses.All(status => status == "reviewed") ? "reviewed"
        : statuses.Any(status => status != "not_reviewed") ? "partially_reviewed" : "not_reviewed";

    private static string Status(ReviewStatus status) => status switch
    {
        ReviewStatus.NotReviewed => "not_reviewed",
        ReviewStatus.PartiallyReviewed => "partially_reviewed",
        ReviewStatus.Reviewed => "reviewed",
        _ => throw InvalidInput()
    };

    private static AccessException InvalidInput() => new(400, "invalid_input", "Некорректная сверка страницы.");
}