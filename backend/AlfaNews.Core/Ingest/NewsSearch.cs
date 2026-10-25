using System.Globalization;
using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

public sealed record DuplicateEvent(Guid EventId, double Similarity);

public sealed record SimilarNewsHit(
    Guid EventId,
    string Headline,
    string? Quote,
    string SourceName,
    string Url,
    DateTimeOffset PublishedAt);

public sealed class NewsSearch(AlfaNewsDbContext db, IConfiguration configuration)
{
    private const int CandidateLimit = 20;
    private const int RrfOffset = 60;
    private static readonly string[] SharedTagKinds = ["factor", "okved2"];

    public async Task<IReadOnlyList<SimilarNewsHit>> SearchAsync(string query, Guid eventId, DateTimeOffset periodFrom,
        DateTimeOffset periodTo, int limit, CancellationToken cancellationToken)
    {
        var minSimilarity = Threshold("Embeddings:SimilarNewsMinSimilarity", 0.86);
        var tags = await db.EventTagRows.AsNoTracking()
            .Where(tag => tag.EventId == eventId && SharedTagKinds.Contains(tag.Kind))
            .Select(tag => tag.Kind + ":" + tag.Code).ToListAsync(cancellationToken);
        var vector = await db.EventRows.AsNoTracking().Where(row => row.Id == eventId).Select(row => row.Embedding)
            .SingleOrDefaultAsync(cancellationToken);
        var window = db.EventRows.AsNoTracking()
            .Where(row => row.Id != eventId && row.PublishedAt >= periodFrom && row.PublishedAt <= periodTo);
        var phrases = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', ';', ':', '!', '?', '«', '»', '"', '(', ')'))
            .Where(word => word.Length > 3)
            .Select(word => "\"" + word.Replace("\"", "", StringComparison.Ordinal) + "\"")
            .Distinct(StringComparer.Ordinal).Take(30).ToArray();
        var ranks = new Dictionary<Guid, double>();

        void Fuse(IReadOnlyList<Guid> ids)
        {
            for (var index = 0; index < ids.Count; index++)
                ranks[ids[index]] = ranks.GetValueOrDefault(ids[index]) + 1d / (RrfOffset + index + 1);
        }

        if (phrases.Length > 0 && tags.Count > 0)
        {
            var text = string.Join(" OR ", phrases);
            Fuse(await window.Where(row => row.SearchText.Matches(EF.Functions.WebSearchToTsQuery("russian", text))
                                           && db.EventTagRows.Any(tag =>
                                               tag.EventId == row.Id && tags.Contains(tag.Kind + ":" + tag.Code)))
                .OrderByDescending(row => row.SearchText.Rank(EF.Functions.WebSearchToTsQuery("russian", text)))
                .ThenBy(row => row.Id)
                .Select(row => row.Id).Take(CandidateLimit).ToListAsync(cancellationToken));
        }

        if (vector is not null)
            Fuse(await window
                .Where(row => row.Embedding != null && row.Embedding!.CosineDistance(vector) <= 1 - minSimilarity)
                .OrderBy(row => row.Embedding!.CosineDistance(vector)).ThenBy(row => row.Id)
                .Select(row => row.Id).Take(CandidateLimit).ToListAsync(cancellationToken));
        var selected = ranks.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
            .Take(Math.Clamp(limit, 1, 5))
            .Select(pair => pair.Key).ToArray();
        var rows = await (from item in db.EventRows.AsNoTracking()
                join news in db.NewsRawRows.AsNoTracking() on item.NewsRawId equals news.Id
                join source in db.SourceRows.AsNoTracking() on news.SourceId equals source.Id
                where selected.Contains(item.Id)
                select new
                {
                    item.Id, item.Headline, item.ExtractionJson, item.PublishedAt, news.CanonicalUrl, source.Name
                })
            .ToListAsync(cancellationToken);
        return selected.Select(id => rows.SingleOrDefault(row => row.Id == id)).Where(row => row is not null)
            .Select(row => new SimilarNewsHit(row!.Id, row.Headline, FirstQuote(row.ExtractionJson), row.Name,
                row.CanonicalUrl, row.PublishedAt))
            .ToArray();
    }

    public async Task<DuplicateEvent?> FindRetellingAsync(Pgvector.Vector vector, DateTimeOffset publishedAt,
        string title, string text,
        CancellationToken cancellationToken)
    {
        // По калибровке 23.10 косинус e5 не отличает пересказ от обновления сюжета, поэтому склейка по умолчанию выключена (порог 1).
        var minSimilarity = Threshold("Embeddings:DuplicateMinSimilarity", 1);
        if (minSimilarity >= 1)
            return null;
        var maxDistance = 1 - minSimilarity;
        var from = publishedAt.AddDays(-2);
        var to = publishedAt.AddDays(1);
        var nearest = await db.EventRows.AsNoTracking()
            .Where(row => row.Embedding != null && row.PublishedAt >= from && row.PublishedAt <= to
                          && row.Embedding!.CosineDistance(vector) <= maxDistance)
            .OrderBy(row => row.Embedding!.CosineDistance(vector)).ThenBy(row => row.Id)
            .Select(row => new
                { row.Id, row.Headline, row.ExtractionJson, Distance = row.Embedding!.CosineDistance(vector) })
            .Take(3).ToListAsync(cancellationToken);
        var numbers = NewsLines.Numbers(title + " " + text);
        var match = nearest.FirstOrDefault(row =>
            numbers.IsSubsetOf(NewsLines.Numbers(row.Headline + " " + row.ExtractionJson.GetRawText())));
        return match is null ? null : new DuplicateEvent(match.Id, Math.Round(1 - match.Distance, 4));
    }

    private double Threshold(string key, double fallback) =>
        double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static string? FirstQuote(JsonElement json)
    {
        try
        {
            return json.Deserialize<NewsExtraction>(LlmOutputValidator.JsonOptions)?.Facts.FirstOrDefault()?.Quote;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}