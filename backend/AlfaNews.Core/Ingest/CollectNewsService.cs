using System.Text.Json;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AlfaNews.Core.Ingest;

public sealed record CollectSummary(string SourceId, int Collected, int Rejected, int Failed, int Skipped, string? Error);

public sealed class CollectNewsService(AlfaNewsDbContext db, SafeFetcher fetcher, TimeProvider timeProvider,
    ILogger<CollectNewsService> logger)
{
    private const int MinFeedTextLength = 400;
    private static readonly TimeSpan NewsWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<CollectSummary>> CollectAsync(IReadOnlyList<string> sourceIds,
        CancellationToken cancellationToken)
    {
        var sources = await db.SourceRows.Where(row => row.Enabled && sourceIds.Contains(row.Id))
            .OrderBy(row => row.Id).ToListAsync(cancellationToken);
        var summaries = new List<CollectSummary>();
        foreach (var source in sources)
        {
            var summary = await CollectSourceAsync(source, cancellationToken);
            logger.LogInformation("Источник {SourceId}: собрано {Collected}, отклонено {Rejected}, ошибок {Failed}, пропущено {Skipped}, сбой ленты {Error}.",
                summary.SourceId, summary.Collected, summary.Rejected, summary.Failed, summary.Skipped, summary.Error ?? "нет");
            summaries.Add(summary);
        }
        return summaries;
    }

    private async Task<CollectSummary> CollectSourceAsync(SourceEntity source, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        source.LastCheckedAt = now;
        var hosts = source.AllowedHosts.Deserialize<string[]>() ?? [];
        IReadOnlyList<FeedEntry> entries;
        try
        {
            var page = await fetcher.GetAsync(source.FeedUrl, hosts, cancellationToken);
            entries = RssReader.Parse(page.Body);
        }
        catch (Exception exception) when (exception is FetchException or System.Xml.XmlException)
        {
            source.LastError = exception is FetchException fetch ? fetch.Code : "invalid_feed";
            await db.SaveChangesAsync(cancellationToken);
            return new CollectSummary(source.Id, 0, 0, 0, 0, source.LastError);
        }

        var recent = entries
            .Where(entry => entry.PublishedAt is null || (entry.PublishedAt >= now - NewsWindow && entry.PublishedAt <= now + ClockSkew))
            .OrderByDescending(entry => entry.PublishedAt ?? DateTimeOffset.MaxValue).ToList();
        var candidates = recent.Select(entry => (Entry: entry, Url: TryCanonical(entry.Url))).Where(pair => pair.Url is not null).ToList();
        var urls = candidates.Select(pair => pair.Url!).ToList();
        var known = (await db.NewsRawRows.AsNoTracking().Where(row => urls.Contains(row.CanonicalUrl))
            .Select(row => row.CanonicalUrl).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        int collected = 0, rejected = 0, failed = 0, skipped = entries.Count - recent.Count;
        foreach (var (entry, url) in candidates)
        {
            if (!known.Add(url!))
            {
                skipped++;
                continue;
            }
            var outcome = await StoreAsync(source, hosts, entry, url!, cancellationToken);
            switch (outcome)
            {
                case StoreOutcome.Collected: collected++; break;
                case StoreOutcome.Rejected: rejected++; break;
                case StoreOutcome.Failed: failed++; break;
                default: skipped++; break;
            }
        }
        source.LastSuccessAt = now;
        source.LastError = failed > 0 && collected == 0 ? "articles_failed" : null;
        await db.SaveChangesAsync(cancellationToken);
        return new CollectSummary(source.Id, collected, rejected, failed, skipped, null);
    }

    private async Task<StoreOutcome> StoreAsync(SourceEntity source, string[] hosts, FeedEntry entry, string url,
        CancellationToken cancellationToken)
    {
        var fetchedAt = timeProvider.GetUtcNow();
        string text;
        var published = entry.PublishedAt?.ToUniversalTime();
        try
        {
            if (entry.FullText is { Length: >= MinFeedTextLength } feedText)
                text = feedText;
            else
            {
                var page = await fetcher.GetAsync(entry.Url, hosts, cancellationToken);
                var article = ArticleExtractor.Extract(page.Body, page.FinalUrl);
                text = article.Text;
                published ??= article.PublishedAt?.ToUniversalTime();
            }
        }
        catch (FetchException exception) when (exception.Transient)
        {
            logger.LogWarning("Источник {SourceId}: временная ошибка {Code}, повтор в следующем цикле.", source.Id, exception.Code);
            return StoreOutcome.Skipped;
        }
        catch (FetchException exception)
        {
            await SaveAsync(new NewsRawEntity
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                CanonicalUrl = url,
                Title = entry.Title,
                Text = string.Empty,
                TextSha256 = TextNormalizer.Sha256(string.Empty),
                PublishedAt = published,
                FetchedAt = fetchedAt,
                Status = "failed",
                LastError = exception.Code
            }, cancellationToken);
            return StoreOutcome.Failed;
        }

        var normalized = TextNormalizer.Normalize(text);
        var rejectedForDate = published is null || published < fetchedAt - NewsWindow || published > fetchedAt + ClockSkew;
        var boilerplate = ArticleExtractor.IsBoilerplateOnly(text);
        var row = new NewsRawEntity
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            CanonicalUrl = url,
            Title = entry.Title,
            Text = text,
            TextSha256 = TextNormalizer.Sha256(normalized),
            PublishedAt = published,
            FetchedAt = fetchedAt,
            Status = rejectedForDate || boilerplate ? "rejected" : "queued",
            Importance = source.IsSpecialized ? 1 : null,
            RejectionReason = rejectedForDate ? (published is null ? "no_published_date" : "outside_window") : boilerplate ? "boilerplate_body" : null
        };
        var saved = await SaveAsync(row, cancellationToken);
        return saved switch
        {
            SaveResult.Saved => rejectedForDate || boilerplate ? StoreOutcome.Rejected : StoreOutcome.Collected,
            SaveResult.Duplicate => StoreOutcome.Skipped,
            _ => StoreOutcome.Failed
        };
    }

    private async Task<SaveResult> SaveAsync(NewsRawEntity row, CancellationToken cancellationToken)
    {
        db.NewsRawRows.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            db.Entry(row).State = EntityState.Detached;
            var postgres = exception.InnerException as PostgresException;
            if (postgres?.SqlState == PostgresErrorCodes.UniqueViolation)
                return SaveResult.Duplicate;
            logger.LogError("Источник {SourceId}: не удалось сохранить статью, {ErrorType}, код {SqlState}, ограничение {Constraint}.",
                row.SourceId, exception.InnerException?.GetType().Name ?? exception.GetType().Name, postgres?.SqlState ?? "n/a",
                postgres?.ConstraintName ?? "n/a");
            return SaveResult.Error;
        }
        return SaveResult.Saved;
    }

    private static string? TryCanonical(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? TextNormalizer.CanonicalUrl(uri) : null;

    private enum StoreOutcome { Collected, Rejected, Failed, Skipped }

    private enum SaveResult { Saved, Duplicate, Error }
}