using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using AlfaNews.Core.Profiles;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

public sealed class NewsProcessing(AlfaNewsDbContext db, LlmGateway gateway, NewsEmbeddings embeddings,
    JobQueue queue, TimeProvider clock)
{
    public async Task<JobResult> ProcessAsync(BackgroundJobEntity job, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.NewsRawRows.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (row is null)
            return new JobResult("missing", ErrorCode: "news_not_found");
        var accepted = await db.EventRows.SingleOrDefaultAsync(item => item.NewsRawId == id, cancellationToken);
        if (accepted is not null)
        {
            await RefreshAsync(clock.GetUtcNow(), cancellationToken);
            return new JobResult("accepted");
        }
        if (row.Status is "duplicate" or "rejected")
            return new JobResult(row.Status);
        var source = await db.SourceRows.AsNoTracking().SingleOrDefaultAsync(source => source.Id == row.SourceId, cancellationToken);
        var now = clock.GetUtcNow();
        var reason = source is null || !source.Enabled ? "source_disabled"
            : row.PublishedAt is null ? "no_published_date"
            : row.PublishedAt < now.AddDays(-7) || row.PublishedAt > now.AddDays(1) ? "outside_window"
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
            var assessment = await embeddings.EvaluateAsync(row.Title, row.Text, cancellationToken);
            row.Embedding = assessment.Vector;
            var similar = await db.EventRows.AsNoTracking().Where(item => item.Embedding != null)
                .OrderBy(item => item.Embedding!.CosineDistance(assessment.Vector))
                .Select(item => new { item.Id, Distance = item.Embedding!.CosineDistance(assessment.Vector) })
                .FirstOrDefaultAsync(cancellationToken);
            row.FilterJson = JsonSerializer.SerializeToElement(new
            {
                stage = "e5",
                assessment.BusinessScore,
                assessment.UnrelatedScore,
                similar_event_id = similar?.Id,
                similarity = similar is null ? (double?)null : 1 - similar.Distance,
                decision = assessment.ClearlyUnrelated ? "clearly_unrelated" : "extract_without_semantic_merge"
            });
            if (assessment.ClearlyUnrelated)
                return await RejectAsync(row, "e5_clearly_unrelated", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            var factors = await db.FactorRows.AsNoTracking().OrderBy(item => item.Code)
                .Select(item => new { code = item.Code, name = item.Name }).ToArrayAsync(cancellationToken);
            var okved2 = await db.SectorSensitivityRows.AsNoTracking().Select(item => item.Okved2).Distinct().Order().ToArrayAsync(cancellationToken);
            var taxonomy = JsonSerializer.SerializeToElement(new
            {
                factors,
                okved2,
                countries = CountryNames.Values,
                regions = Array.Empty<string>(),
                commodities = Array.Empty<string>()
            });
            var input = JsonSerializer.SerializeToElement(new { title = row.Title, text = row.Text, published_at = row.PublishedAt });
            var version = LlmPromptCatalog.Hash(input.GetRawText() + taxonomy.GetRawText());
            var result = await gateway.GenerateAsync<NewsExtraction>(new LlmRequest(job.Id, "news-extract:" + row.TextSha256,
                version, LlmTask.NewsExtract, new Dictionary<string, JsonElement>
                {
                    ["INPUT_JSON"] = input,
                    ["TAXONOMY_JSON"] = taxonomy
                }), cancellationToken);
            var extraction = result.Value;
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                var eventId = Guid.NewGuid();
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
                    Embedding = assessment.Vector,
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
                await RefreshAsync(now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
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

    private async Task<JobResult> RejectAsync(NewsRawEntity row, string reason, CancellationToken cancellationToken)
    {
        row.Status = "rejected";
        row.RejectionReason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return new JobResult("rejected");
    }

    private async Task RefreshAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var companies = await db.CompanyProfileRows.AsNoTracking().Select(row => row.CompanyId).ToArrayAsync(cancellationToken);
        foreach (var companyId in companies)
        {
            if (!await db.BackgroundJobRows.AnyAsync(row => row.Kind == "RefreshFeed" && row.CompanyId == companyId
                    && (row.Status == "queued" || row.Status == "running"), cancellationToken))
                await queue.EnqueueAsync(new RefreshFeedJob(companyId, asOf), null, companyId, cancellationToken);
        }
    }
}