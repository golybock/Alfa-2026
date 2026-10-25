using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace AlfaNews.Tests;

public sealed class FeedRefreshDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task RefreshRanksWeeklyEventsStoresSnapshotAndQueuesBothRoles()
    {
        await using var db = fixture.CreateDb();
        await new TaxonomyCatalogLoader(db).LoadAsync(Path.Combine(AppContext.BaseDirectory, "taxonomy.json"),
            CancellationToken.None);
        Assert.True(await db.SectorSensitivityRows.AnyAsync(row => row.Okved2 == "41" && row.FactorCode == "key_rate"));
        await new TaxonomyCatalogLoader(db).LoadAsync(Path.Combine(AppContext.BaseDirectory, "taxonomy.json"),
            CancellationToken.None);
        var options = await EnsureCompanyAsync(db);
        var asOf = DateTimeOffset.UtcNow;
        var relevant = await AddEventAsync(db, "rate", asOf.AddHours(-2), ["key_rate"]);
        await AddEventAsync(db, "unrelated", asOf.AddHours(-1), ["timber"]);
        var old = await AddEventAsync(db, "old", asOf.AddDays(-9), ["key_rate"]);
        var service = new FeedRefreshService(db, new JobQueue(db, TimeProvider.System), new RankingOptions(),
            TimeProvider.System);
        var result = await service.RefreshAsync(new RefreshFeedJob("test_company_a", asOf), CancellationToken.None);
        Assert.Null(result.ErrorCode);
        Assert.Equal("feed", result.ResultType);
        var snapshot = await db.FeedSnapshotRows.AsNoTracking().SingleAsync(row => row.CompanyId == "test_company_a");
        var items = snapshot.ItemsJson.Deserialize<List<FeedItemDto>>(options)!;
        var item = Assert.Single(items);
        Assert.Equal(relevant.ToString(), item.EventId);
        Assert.Equal(1, item.Relevance.Rank);
        Assert.True(item.Relevance.Score >= 0.37m);
        Assert.Equal(FeedRanking.WindowStart(asOf), snapshot.NewsPeriodFrom);
        var jobs = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == "GenerateInsight" && row.CompanyId == "test_company_a")
            .ToListAsync();
        Assert.Equal(new[] { Role.Ceo, Role.Cfo },
            jobs.Select(job => job.Payload.Deserialize<GenerateInsightJob>(JobKinds.JsonOptions)!.Role).Order());
        await service.RefreshAsync(new RefreshFeedJob("test_company_a", asOf), CancellationToken.None);
        Assert.Equal(2,
            await db.BackgroundJobRows.CountAsync(row =>
                row.Kind == "GenerateInsight" && row.CompanyId == "test_company_a"));
        var feed = await Feed(db, new FeedSettings())
            .GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        Assert.Equal(relevant.ToString(), Assert.Single(feed.Items).EventId);
        Assert.Equal(AnalysisStatus.Queued, feed.Items[0].AnalysisStatus);

        var demoAsOf = asOf.AddDays(-8);
        var demo = await new FeedRefreshService(db, new JobQueue(db, TimeProvider.System),
                new RankingOptions { PrecomputeInsights = false }, TimeProvider.System)
            .PrecomputeDemoAsync(new PrecomputeDemoJob(["test_company_a", "test_company_b"], demoAsOf),
                CancellationToken.None);
        Assert.Equal(("partial", null), (demo.Stage, demo.ErrorCode));
        var demoFeed = await Feed(db, new FeedSettings { Mode = FeedSettings.Demo })
            .GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        Assert.Equal(old.ToString(), Assert.Single(demoFeed.Items).EventId);
        Assert.True((demoFeed.AsOf - demoAsOf).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Equal(relevant.ToString(), Assert.Single((await Feed(db, new FeedSettings()).GetAsync(fixture.User,
            "test_company_a", Role.Cfo,
            CancellationToken.None)).Items).EventId);
        Assert.Equal(2, await db.BackgroundJobRows.CountAsync(row => row.Kind == "GenerateInsight" &&
                                                                     row.CompanyId == "test_company_a"
                                                                     && row.Payload.GetProperty("event_id")
                                                                         .GetString() == old.ToString()));
        Assert.Throws<InvalidOperationException>(() => new FeedSettings { Mode = "archive" }.Validate());
        var missing = await service.RefreshAsync(new RefreshFeedJob("test_company_b", asOf), CancellationToken.None);
        Assert.Equal("profile_not_found", missing.ErrorCode);
    }

    [PostgresFact]
    public async Task AcceptedEventQueuesRefreshOnlyForTargetCompaniesItPasses()
    {
        await using var db = fixture.CreateDb();
        await new TaxonomyCatalogLoader(db).LoadAsync(Path.Combine(AppContext.BaseDirectory, "taxonomy.json"),
            CancellationToken.None);
        await EnsureCompanyAsync(db);
        await db.BackgroundJobRows.Where(row => row.Kind == "RefreshFeed").ExecuteDeleteAsync();
        var now = DateTimeOffset.UtcNow;
        var relevant = await AddEventAsync(db, "target-rate", now.AddHours(-1), ["key_rate"]);
        var unrelated = await AddEventAsync(db, "target-timber", now.AddHours(-1), ["timber"]);
        var service = new FeedRefreshService(db, new JobQueue(db, TimeProvider.System),
            new RankingOptions { CompanyIds = ["test_company_a", "test_company_missing"] }, TimeProvider.System);
        Assert.Equal(0, await service.EnqueueForEventAsync(unrelated, CancellationToken.None));
        Assert.Equal(1, await service.EnqueueForEventAsync(relevant, CancellationToken.None));
        Assert.Equal(0, await service.EnqueueForEventAsync(relevant, CancellationToken.None));
        Assert.Equal("test_company_a",
            (await db.BackgroundJobRows.AsNoTracking().SingleAsync(row => row.Kind == "RefreshFeed")).CompanyId);
        var untargeted = new FeedRefreshService(db, new JobQueue(db, TimeProvider.System), new RankingOptions(),
            TimeProvider.System);
        Assert.Equal(0, await untargeted.EnqueueForEventAsync(relevant, CancellationToken.None));
        await db.EventTagRows.Where(row => row.EventId == relevant || row.EventId == unrelated).ExecuteDeleteAsync();
        await db.EventRows.Where(row => row.Id == relevant || row.Id == unrelated).ExecuteDeleteAsync();
    }

    private static FeedService Feed(AlfaNews.Core.Data.AlfaNewsDbContext db, FeedSettings settings) =>
        new(db, new AccessService(db, TimeProvider.System), new JobQueue(db, TimeProvider.System), settings,
            TimeProvider.System);

    private static async Task<JsonSerializerOptions> EnsureCompanyAsync(AlfaNews.Core.Data.AlfaNewsDbContext db)
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        var empty = new PaymentExposure(0, 0, 0, new Dictionary<string, decimal>());
        if (!await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == "test_company_a"))
            db.CompanyProfileRows.Add(new CompanyProfileEntity
            {
                CompanyId = "test_company_a",
                DataVersion = "test",
                ProfileJson = JsonSerializer.SerializeToElement(new CompanyProfile("test_company_a", null, "41.20",
                    "41", "Строительство",
                    null, null, null, null, null, ["loan"], [], empty, empty, "test"), options),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();
        return options;
    }

    private static async Task<Guid> AddEventAsync(AlfaNews.Core.Data.AlfaNewsDbContext db, string key,
        DateTimeOffset publishedAt, string[] factors)
    {
        var text = "Синтетическая новость " + key + " " + Guid.NewGuid();
        var news = new NewsRawEntity
        {
            Id = Guid.NewGuid(),
            SourceId = "synthetic",
            CanonicalUrl = "https://example.org/feed-test/" + Guid.NewGuid(),
            Title = key,
            Text = text,
            TextSha256 = TextNormalizer.Sha256(text),
            PublishedAt = publishedAt,
            FetchedAt = publishedAt,
            Status = "accepted",
            Importance = 1
        };
        var extraction = new NewsExtraction(key, null, [new ExtractedFact(text, text)], [],
            new NewsTags(factors, [], [], [], []));
        var eventId = Guid.NewGuid();
        db.NewsRawRows.Add(news);
        db.EventRows.Add(new EventEntity
        {
            Id = eventId,
            NewsRawId = news.Id,
            ContentSha256 = news.TextSha256,
            Headline = key,
            PublishedAt = publishedAt,
            ExtractionJson = JsonSerializer.SerializeToElement(extraction, LlmOutputValidator.JsonOptions),
            InputVersion = "v-" + key,
            CreatedAt = publishedAt,
            SearchText = NpgsqlTsVector.Empty
        });
        db.EventTagRows.AddRange(factors.Select(code => new EventTagEntity
            { EventId = eventId, Kind = "factor", Code = code }));
        await db.SaveChangesAsync();
        return eventId;
    }
}