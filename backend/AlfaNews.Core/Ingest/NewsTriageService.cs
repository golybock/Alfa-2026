using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

public sealed class NewsTriageService(AlfaNewsDbContext db, LlmGateway gateway, NewsProcessingSchedule schedule, NewsEmbeddings embeddings,
    NewsSearch search)
{
    public async Task ProcessAsync(BackgroundJobEntity job, TriageNewsJob payload, CancellationToken cancellationToken)
    {
        if (payload.NewsRawIds.Count is < 1 or > NewsProcessingSchedule.BatchSize
            || payload.NewsRawIds.Distinct().Count() != payload.NewsRawIds.Count)
            throw new JsonException();
        var ids = payload.NewsRawIds.Select(Guid.Parse).ToArray();
        var rows = await db.NewsRawRows.Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        if (rows.Length == 0 || rows.All(row => row.Importance != null || row.Status is not ("queued" or "failed")))
            return;
        foreach (var row in rows.Where(row => row.Importance == null && row.Status == "failed"))
        {
            row.Status = "queued";
            row.LastError = null;
        }
        foreach (var row in rows.Where(row => row.Importance == null && row.Status == "queued"))
            await ScreenAsync(row, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        rows = rows.Where(row => row.Importance == null && row.Status == "queued").ToArray();
        if (rows.Length == 0)
        {
            await schedule.EnqueueAsync(cancellationToken);
            return;
        }
        var input = JsonSerializer.SerializeToElement(rows.Select(row => new { id = row.Id.ToString(), title = row.Title }));
        var version = LlmPromptCatalog.Hash(input.GetRawText());
        try
        {
            var result = await gateway.GenerateAsync<NewsTriage>(new LlmRequest(job.Id, "news-triage:" + version,
                version, LlmTask.NewsTriage, new Dictionary<string, JsonElement> { ["INPUT_JSON"] = input }), cancellationToken);
            foreach (var decision in result.Value.Decisions)
            {
                var row = rows.Single(row => row.Id.ToString() == decision.Id);
                if (row.Importance != null || row.Status != "queued")
                    continue;
                row.Importance = decision.Importance switch { NewsImportance.Yes => 1, NewsImportance.Maybe => 2, _ => 3 };
                row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "headline_triage", run_id = result.RunId, importance = row.Importance });
                if (row.Importance == 3)
                {
                    row.Status = "rejected";
                    row.RejectionReason = "headline_irrelevant";
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (LlmException exception)
        {
            foreach (var row in rows.Where(row => row.Importance == null && row.Status == "queued"))
            {
                row.Status = "failed";
                row.LastError = exception.Code;
            }
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        await schedule.EnqueueAsync(cancellationToken);
    }

    private async Task ScreenAsync(NewsRawEntity row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.Text) || row.PublishedAt is null)
            return;
        NewsEmbeddingResult assessment;
        try
        {
            assessment = await embeddings.EvaluateAsync(row.Title, row.Text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return;
        }
        row.Embedding = assessment.Vector;
        if (assessment.ClearlyUnrelated)
        {
            row.Status = "rejected";
            row.RejectionReason = "e5_clearly_unrelated";
            row.FilterJson = JsonSerializer.SerializeToElement(new
            {
                stage = "e5",
                assessment.BusinessScore,
                assessment.UnrelatedScore,
                decision = "clearly_unrelated"
            });
            return;
        }
        if (await search.FindRetellingAsync(assessment.Vector, row.PublishedAt.Value, row.Title, row.Text, cancellationToken) is not { } retelling)
            return;
        if (!await db.EventSourceRows.AnyAsync(link => link.EventId == retelling.EventId && link.NewsRawId == row.Id, cancellationToken))
            db.EventSourceRows.Add(new EventSourceEntity { EventId = retelling.EventId, NewsRawId = row.Id });
        row.Status = "duplicate";
        row.RejectionReason = "semantic_duplicate";
        row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "e5_duplicate", event_id = retelling.EventId, retelling.Similarity });
    }
}