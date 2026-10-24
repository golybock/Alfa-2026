using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using AlfaNews.Core.Profiles;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

public sealed class NewsProcessing(AlfaNewsDbContext db, LlmGateway gateway, NewsEmbeddings embeddings, NewsSearch search,
    FeedRefreshService feed, TimeProvider clock)
{
    public async Task<JobResult> ProcessAsync(BackgroundJobEntity job, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.NewsRawRows.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (row is null)
            return new JobResult("missing", ErrorCode: "news_not_found");
        var accepted = await db.EventRows.SingleOrDefaultAsync(item => item.NewsRawId == id, cancellationToken);
        if (accepted is not null)
            return new JobResult("accepted");
        if (row.Status is "duplicate" or "rejected")
            return new JobResult(row.Status);
        var source = await db.SourceRows.AsNoTracking().SingleOrDefaultAsync(source => source.Id == row.SourceId, cancellationToken);
        var now = clock.GetUtcNow();
        var reason = source is null || !source.Enabled ? "source_disabled"
            : row.PublishedAt is null ? "no_published_date"
            : row.PublishedAt < await NewsWindow.StartAsync(db, now, cancellationToken) || row.PublishedAt > NewsWindow.End(now) ? "outside_window"
            : ArticleExtractor.IsBoilerplateOnly(row.Text) ? "boilerplate_body"
            : string.IsNullOrWhiteSpace(row.Text) ? "empty_body"
            : row.Importance == 3 ? "headline_irrelevant" : null;
        if (reason is not null)
            return await RejectAsync(row, reason, cancellationToken);
        if (row.Importance == null && source!.IsSpecialized)
            row.Importance = 1;
        if (row.Importance is not (1 or 2))
            return new JobResult("awaiting_triage");
        row.Status = "processing";
        row.LastError = null;
        row.TextSha256 = TextNormalizer.Sha256(TextNormalizer.Normalize(row.Text));
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var duplicate = await db.EventRows.SingleOrDefaultAsync(item => item.ContentSha256 == row.TextSha256, cancellationToken);
            if (duplicate is not null)
            {
                if (!await db.EventSourceRows.AnyAsync(link => link.EventId == duplicate.Id && link.NewsRawId == id, cancellationToken))
                    db.EventSourceRows.Add(new EventSourceEntity { EventId = duplicate.Id, NewsRawId = id });
                row.Status = "duplicate";
                row.RejectionReason = "exact_content_duplicate";
                row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "exact_duplicate", event_id = duplicate.Id });
                await db.SaveChangesAsync(cancellationToken);
                return new JobResult("duplicate");
            }
            var assessment = row.Embedding is null ? await AssessAsync(row, cancellationToken) : null;
            row.Embedding ??= assessment?.Vector;
            if (assessment is { ClearlyUnrelated: true })
            {
                row.FilterJson = JsonSerializer.SerializeToElement(new
                {
                    stage = "e5",
                    assessment.BusinessScore,
                    assessment.UnrelatedScore,
                    decision = "clearly_unrelated"
                });
                return await RejectAsync(row, "e5_clearly_unrelated", cancellationToken);
            }
            if (row.Embedding is { } vector)
            {
                if (await search.FindRetellingAsync(vector, row.PublishedAt!.Value, row.Title, row.Text, cancellationToken) is { } retelling)
                {
                    if (!await db.EventSourceRows.AnyAsync(link => link.EventId == retelling.EventId && link.NewsRawId == id, cancellationToken))
                        db.EventSourceRows.Add(new EventSourceEntity { EventId = retelling.EventId, NewsRawId = id });
                    row.Status = "duplicate";
                    row.RejectionReason = "semantic_duplicate";
                    row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "e5_duplicate", event_id = retelling.EventId, retelling.Similarity });
                    await db.SaveChangesAsync(cancellationToken);
                    return new JobResult("duplicate");
                }
                row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "e5", decision = "extract_without_semantic_merge" });
            }
            else
                row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "e5", decision = "e5_unavailable" });
            await db.SaveChangesAsync(cancellationToken);
            var factors = await db.FactorRows.AsNoTracking().OrderBy(item => item.Code)
                .Select(item => new { code = item.Code, name = item.Name }).ToArrayAsync(cancellationToken);
            var okved2 = Okved2Names.All.Select(pair => new { code = pair.Key, name = pair.Value }).ToArray();
            var taxonomy = JsonSerializer.SerializeToElement(new
            {
                factors,
                okved2,
                countries = CountryNames.Values,
                regions = Array.Empty<string>(),
                commodities = Array.Empty<string>()
            });
            var lines = NewsLines.Split(row.Text);
            var input = JsonSerializer.SerializeToElement(new { title = row.Title, lines = NewsLines.Numbered(lines), published_at = row.PublishedAt });
            var version = LlmPromptCatalog.Hash(input.GetRawText() + taxonomy.GetRawText());
            var result = await gateway.GenerateAsync<NewsExtractionDraft>(new LlmRequest(job.Id, "news-extract:" + row.TextSha256,
                version, LlmTask.NewsExtract, new Dictionary<string, JsonElement>
                {
                    ["INPUT_JSON"] = input,
                    ["TAXONOMY_JSON"] = taxonomy
                }), cancellationToken);
            var draft = result.Value;
            var extraction = new NewsExtraction(draft.Headline, draft.EventDate, draft.Facts.Select(fact =>
                new ExtractedFact(fact.Text, NewsLines.Resolve(lines, fact).Quote!, fact.Line, fact.Start, fact.End)).ToArray(),
                draft.Participants, draft.Tags);
            var eventId = Guid.NewGuid();
            try
            {
                await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
                {
                    db.EventRows.Add(new EventEntity
                    {
                        Id = eventId,
                        NewsRawId = id,
                        ContentSha256 = row.TextSha256,
                        Headline = extraction.Headline,
                        EventDate = extraction.EventDate,
                        PublishedAt = row.PublishedAt!.Value,
                        ExtractionJson = JsonSerializer.SerializeToElement(extraction, LlmOutputValidator.JsonOptions),
                        InputVersion = version,
                        Embedding = row.Embedding,
                        CreatedAt = now,
                        SearchText = NpgsqlTsVector.Empty
                    });
                    db.EventSourceRows.Add(new EventSourceEntity { EventId = eventId, NewsRawId = id });
                    foreach (var (kind, codes) in new[]
                             {
                                 ("factor", extraction.Tags.Factors), ("okved2", extraction.Tags.Okved2),
                                 ("country", extraction.Tags.Countries), ("region", extraction.Tags.Regions), ("commodity", extraction.Tags.Commodities)
                             })
                        foreach (var code in codes.Distinct(StringComparer.Ordinal))
                            db.EventTagRows.Add(new EventTagEntity { EventId = eventId, Kind = kind, Code = code });
                    row.Status = "accepted";
                    row.RejectionReason = null;
                    await db.SaveChangesAsync(cancellationToken);
                    var search = extraction.Headline + " " + string.Join(" ", extraction.Facts.Select(fact => fact.Text));
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event SET search_text = to_tsvector('russian', {search}) WHERE id = {eventId}", cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            // Тот же текст из другого источника разобран параллельно и сохранён раньше: это точный дубль, а не сбой.
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "event_content_sha256_key" })
            {
                db.ChangeTracker.Clear();
                return await LinkDuplicateAsync(id, cancellationToken);
            }
            await feed.EnqueueForEventAsync(eventId, cancellationToken);
            return new JobResult("accepted");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureDetails = JsonSerializer.SerializeToElement(new
            {
                stage = "extraction_failed",
                previous_filter = row.FilterJson,
                diagnostics = exception is LlmException llm ? llm.Diagnostics : Array.Empty<string>()
            });
            db.ChangeTracker.Clear();
            await db.NewsRawRows.Where(item => item.Id == id).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "failed").SetProperty(item => item.FilterJson, (JsonElement?)failureDetails)
                .SetProperty(item => item.LastError,
                    exception is LlmException ? ((LlmException)exception).Code : "news_processing_failed"), cancellationToken);
            throw;
        }
    }

    private async Task<JobResult> LinkDuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.NewsRawRows.SingleAsync(item => item.Id == id, cancellationToken);
        var duplicate = await db.EventRows.AsNoTracking().SingleAsync(item => item.ContentSha256 == row.TextSha256, cancellationToken);
        if (!await db.EventSourceRows.AnyAsync(link => link.EventId == duplicate.Id && link.NewsRawId == id, cancellationToken))
            db.EventSourceRows.Add(new EventSourceEntity { EventId = duplicate.Id, NewsRawId = id });
        row.Status = "duplicate";
        row.RejectionReason = "exact_content_duplicate";
        row.FilterJson = JsonSerializer.SerializeToElement(new { stage = "exact_duplicate", event_id = duplicate.Id });
        await db.SaveChangesAsync(cancellationToken);
        return new JobResult("duplicate");
    }

    private async Task<JobResult> RejectAsync(NewsRawEntity row, string reason, CancellationToken cancellationToken)
    {
        row.Status = "rejected";
        row.RejectionReason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return new JobResult("rejected");
    }

    private async Task<NewsEmbeddingResult?> AssessAsync(NewsRawEntity row, CancellationToken cancellationToken)
    {
        try
        {
            return await embeddings.EvaluateAsync(row.Title, row.Text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}