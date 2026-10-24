using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Core.Documents;

public sealed record ResearchHit(
    Guid ChunkId,
    Guid DocumentId,
    string Title,
    DateOnly? PublishedOn,
    int PageNo,
    ResearchBlockKind Kind,
    string Text,
    ReviewStatus ReviewStatus,
    IReadOnlyList<ChartDraft> ReviewedCharts);

public sealed class ResearchSearch(AlfaNewsDbContext db, NewsEmbeddings embeddings)
{
    private const int CandidateLimit = 20;
    private const int RrfOffset = 60;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<JobResult> IndexAsync(EmbedContentJob payload, CancellationToken cancellationToken)
    {
        if (payload.EntityType != "research_chunk" || !Guid.TryParse(payload.EntityId, out var id))
            return new JobResult("rejected", ErrorCode: "invalid_payload");
        var chunk = await db.ResearchChunkRows.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (chunk is null)
            return new JobResult("missing", ErrorCode: "chunk_not_found");
        if (chunk.Version != payload.Version)
            return new JobResult("stale");
        var title = await (from page in db.ResearchPageRows.AsNoTracking()
                           join document in db.ResearchDocRows.AsNoTracking() on page.DocumentId equals document.Id
                           where page.Id == chunk.PageId
                           select document.Title).SingleAsync(cancellationToken);
        var tables = chunk.TablesJson.Deserialize<List<TableDraft>>(JsonOptions) ?? [];
        var charts = chunk.ChartData.Deserialize<List<ReviewableChartDto>>(JsonOptions) ?? [];
        var text = string.Join("\n", new[] { title, chunk.Text }
            .Concat(tables.Select(table => table.Title + ": " + string.Join("; ", table.Columns)))
            .Concat(charts.Select(chart => chart.Chart.Title + ". " + chart.Chart.Description)));
        chunk.Embedding = await embeddings.EmbedPassageAsync(text, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new JobResult("indexed");
    }

    public async Task<IReadOnlyList<ResearchHit>> SearchAsync(SearchResearchArguments arguments, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(arguments.Limit, 1, 6);
        var factors = await db.FactorRows.AsNoTracking().Where(row => arguments.Factors.Contains(row.Code))
            .Select(row => new { row.Name, row.Keywords }).ToListAsync(cancellationToken);
        var phrases = arguments.Query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', ';', ':', '!', '?', '«', '»', '"', '(', ')')).Where(word => word.Length > 3)
            .Concat(factors.Select(factor => factor.Name))
            .Concat(factors.SelectMany(factor => factor.Keywords.Deserialize<string[]>() ?? []))
            .Select(phrase => "\"" + phrase.Replace("\"", "", StringComparison.Ordinal) + "\"")
            .Distinct(StringComparer.Ordinal).Take(40).ToArray();
        var ranks = new Dictionary<Guid, double>();
        void Fuse(IReadOnlyList<Guid> ids)
        {
            for (var index = 0; index < ids.Count; index++)
                ranks[ids[index]] = ranks.GetValueOrDefault(ids[index]) + 1d / (RrfOffset + index + 1);
        }
        if (phrases.Length > 0)
        {
            var query = string.Join(" OR ", phrases);
            Fuse(await db.ResearchChunkRows.AsNoTracking()
                .Where(row => row.SearchText.Matches(EF.Functions.WebSearchToTsQuery("russian", query)))
                .OrderByDescending(row => row.SearchText.Rank(EF.Functions.WebSearchToTsQuery("russian", query))).ThenBy(row => row.Id)
                .Select(row => row.Id).Take(CandidateLimit).ToListAsync(cancellationToken));
        }
        var vector = await QueryVectorAsync(arguments.Query + " " + string.Join(", ", factors.Select(factor => factor.Name)), cancellationToken);
        if (vector is not null)
            Fuse(await db.ResearchChunkRows.AsNoTracking().Where(row => row.Embedding != null)
                .OrderBy(row => row.Embedding!.CosineDistance(vector)).ThenBy(row => row.Id)
                .Select(row => row.Id).Take(CandidateLimit).ToListAsync(cancellationToken));
        var selected = ranks.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(limit).Select(pair => pair.Key).ToArray();
        var rows = await (from chunk in db.ResearchChunkRows.AsNoTracking()
                          join page in db.ResearchPageRows.AsNoTracking() on chunk.PageId equals page.Id
                          join document in db.ResearchDocRows.AsNoTracking() on page.DocumentId equals document.Id
                          where selected.Contains(chunk.Id) && page.OcrStatus == "ready"
                          select new { chunk.Id, DocumentId = document.Id, document.Title, document.PublishedOn, page.PageNo, chunk.Kind, chunk.Text, chunk.ReviewStatus, chunk.ChartData })
            .ToListAsync(cancellationToken);
        return selected.Select(id => rows.SingleOrDefault(row => row.Id == id)).Where(row => row is not null).Select(row => new ResearchHit(
            row!.Id, row.DocumentId, row.Title, row.PublishedOn, row.PageNo,
            JsonSerializer.Deserialize<ResearchBlockKind>(JsonSerializer.Serialize(row.Kind), JsonOptions),
            row.Text, JsonSerializer.Deserialize<ReviewStatus>(JsonSerializer.Serialize(row.ReviewStatus), JsonOptions),
            (row.ChartData.Deserialize<List<ReviewableChartDto>>(JsonOptions) ?? [])
                .Where(chart => chart.ReviewStatus == ReviewStatus.Reviewed).Select(chart => chart.Chart).ToArray())).ToArray();
    }

    public async Task<IReadOnlySet<Guid>> MatchingFactorsAsync(IReadOnlyCollection<Guid> chunkIds, IReadOnlyCollection<string> factors,
        CancellationToken cancellationToken)
    {
        var query = await TopicQueryAsync(factors, cancellationToken);
        if (query is null || chunkIds.Count == 0)
            return new HashSet<Guid>();
        return (await db.ResearchChunkRows.AsNoTracking()
            .Where(row => chunkIds.Contains(row.Id) && row.SearchText.Matches(EF.Functions.WebSearchToTsQuery("russian", query)))
            .Select(row => row.Id).ToListAsync(cancellationToken)).ToHashSet();
    }

    // Есть ли обзор банка на ту же тему, опубликованный позже даты и не позже asOf: тогда более старая оценка устарела.
    public async Task<bool> LaterOnTopicAsync(DateOnly after, DateOnly asOf, IReadOnlyCollection<string> factors,
        CancellationToken cancellationToken)
    {
        var query = await TopicQueryAsync(factors, cancellationToken);
        if (query is null)
            return false;
        return await (from chunk in db.ResearchChunkRows.AsNoTracking()
                      join page in db.ResearchPageRows.AsNoTracking() on chunk.PageId equals page.Id
                      join document in db.ResearchDocRows.AsNoTracking() on page.DocumentId equals document.Id
                      where document.PublishedOn > after && document.PublishedOn <= asOf
                          && chunk.SearchText.Matches(EF.Functions.WebSearchToTsQuery("russian", query))
                      select chunk.Id).AnyAsync(cancellationToken);
    }

    private async Task<string?> TopicQueryAsync(IReadOnlyCollection<string> factors, CancellationToken cancellationToken)
    {
        var phrases = (await db.FactorRows.AsNoTracking().Where(row => factors.Contains(row.Code))
                .Select(row => new { row.Name, row.Keywords }).ToListAsync(cancellationToken))
            .SelectMany(factor => (factor.Keywords.Deserialize<string[]>() ?? []).Append(factor.Name))
            .Select(phrase => "\"" + phrase.Replace("\"", "", StringComparison.Ordinal) + "\"").Distinct(StringComparer.Ordinal).ToArray();
        return phrases.Length == 0 ? null : string.Join(" OR ", phrases);
    }

    private async Task<Pgvector.Vector?> QueryVectorAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            return await embeddings.EmbedQueryAsync(text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}