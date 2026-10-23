using System.Text.Json;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlfaNews.Tests;

public sealed class CollectNewsServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task CollectsMoreThanThirtyArticlesAndMarksSpecializedSourceImportant()
    {
        var now = DateTimeOffset.UtcNow;
        var feed = "";
        using var server = new LocalHttpServer(_ => new LocalResponse(200, feed, "application/xml"));
        feed = "<rss><channel>" + string.Concat(Enumerable.Range(0, 40).Select(index =>
            $"<item><title>Новость {index}</title><link>{server.BaseUrl}/article/{index}</link><pubDate>{now:R}</pubDate><full-text>{new string('а', 500)}</full-text></item>")) + "</channel></rss>";
        await using var db = fixture.CreateDb();
        var source = new SourceEntity
        {
            Id = "forty_articles",
            Name = "Синтетический",
            FeedUrl = server.BaseUrl + "/rss",
            Kind = "media",
            AllowedHosts = JsonSerializer.SerializeToElement(new[] { "127.0.0.1" }),
            Enabled = true,
            IsSpecialized = true,
            Reliability = 1,
            ValidationStatus = "checked"
        };
        db.SourceRows.Add(source);
        await db.SaveChangesAsync();
        using var fetcher = new SafeFetcher(true, TimeSpan.Zero);
        var service = new CollectNewsService(db, fetcher, TimeProvider.System, NullLogger<CollectNewsService>.Instance);
        Assert.Equal(40, (await service.CollectAsync([source.Id], CancellationToken.None)).Single().Collected);
        Assert.All(await db.NewsRawRows.Where(row => row.SourceId == source.Id).ToArrayAsync(), row => Assert.Equal(1, row.Importance));
        Assert.Equal(0, (await service.CollectAsync([source.Id], CancellationToken.None)).Single().Collected);
    }

    private const string ArticleText = "Синтетический абзац для проверки сбора новостей из ленты источника и сохранения полного текста статьи. ";

    [PostgresFact]
    public async Task CollectsFreshArticlesOncePerUrlAndKeepsOtherSourcesAlive()
    {
        var now = DateTimeOffset.UtcNow;
        string Rfc(DateTimeOffset date) => date.ToOffset(TimeSpan.FromHours(3)).ToString("ddd, dd MMM yyyy HH:mm:ss +0300", System.Globalization.CultureInfo.InvariantCulture);
        string Article(string body, string? time = null) =>
            $"<html><body><article><h1>Заголовок</h1>{(time is null ? "" : $"<time datetime=\"{time}\">дата</time>")}<p>{string.Concat(Enumerable.Repeat(body, 4))}</p></article></body></html>";
        var feed = $"""
            <rss version="2.0"><channel>
            <item><title>Из ленты</title><link>@/inline?utm_source=rss</link><pubDate>{Rfc(now.AddHours(-1))}</pubDate>
              <full-text>{string.Concat(Enumerable.Repeat(ArticleText, 6))}</full-text></item>
            <item><title>Со страницы</title><link>@/page</link><pubDate>{Rfc(now.AddHours(-2))}</pubDate></item>
            <item><title>Старая</title><link>@/old</link><pubDate>{Rfc(now.AddDays(-12))}</pubDate></item>
            <item><title>Без даты в ленте</title><link>@/nodate</link></item>
            <item><title>Пустая страница</title><link>@/empty</link><pubDate>{Rfc(now.AddHours(-3))}</pubDate></item>
            <item><title>Сбой сервера</title><link>@/flaky</link><pubDate>{Rfc(now.AddHours(-4))}</pubDate></item>
            </channel></rss>
            """;
        var feedBody = string.Empty;
        using var live = new LocalHttpServer(path => path switch
        {
            "/feed.xml" => new LocalResponse(200, feedBody, "application/xml"),
            "/page" => new LocalResponse(200, Article(ArticleText)),
            "/nodate" => new LocalResponse(200, Article(ArticleText)),
            "/empty" => new LocalResponse(200, "<html><body><p>Пусто</p></body></html>"),
            "/broken.xml" => new LocalResponse(200, "<rss><channel><item>", "application/xml"),
            _ => new LocalResponse(503, "busy")
        });
        feedBody = feed.Replace("@/", live.BaseUrl + "/");

        await using var db = fixture.CreateDb();
        var ids = new[] { "ingest_ok", "ingest_broken", "ingest_foreign" };
        foreach (var (id, url, hosts) in new[]
                 {
                     (ids[0], live.BaseUrl + "/feed.xml", new[] { "127.0.0.1" }),
                     (ids[1], live.BaseUrl + "/broken.xml", new[] { "127.0.0.1" }),
                     (ids[2], live.BaseUrl + "/feed.xml", new[] { "example.org" })
                 })
            db.SourceRows.Add(new SourceEntity
            {
                Id = id,
                Name = id,
                FeedUrl = url,
                Kind = "media",
                AllowedHosts = JsonSerializer.SerializeToElement(hosts),
                Enabled = true,
                Reliability = 0.9m,
                ValidationStatus = "checked"
            });
        await db.SaveChangesAsync();

        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);
        var service = new CollectNewsService(db, fetcher, TimeProvider.System,
            NullLogger<CollectNewsService>.Instance);

        var summaries = await service.CollectAsync(ids, CancellationToken.None);
        var ok = summaries.Single(summary => summary.SourceId == ids[0]);
        Assert.Equal(2, ok.Collected);
        Assert.Equal(1, ok.Rejected);
        Assert.Equal(1, ok.Failed);
        Assert.NotNull(summaries.Single(summary => summary.SourceId == ids[1]).Error);
        Assert.Equal("host_not_allowed", summaries.Single(summary => summary.SourceId == ids[2]).Error);

        var rows = await db.NewsRawRows.AsNoTracking().Where(row => row.SourceId == ids[0]).ToListAsync();
        Assert.Equal(["failed", "queued", "queued", "rejected"], rows.Select(row => row.Status).Order().ToArray());
        var inline = rows.Single(row => row.Title == "Из ленты");
        Assert.Equal(live.BaseUrl + "/inline", inline.CanonicalUrl);
        Assert.Equal(TextNormalizer.Sha256(TextNormalizer.Normalize(inline.Text)), inline.TextSha256);
        Assert.Equal(TimeSpan.Zero, inline.PublishedAt!.Value.Offset);
        Assert.InRange(inline.PublishedAt.Value, now.AddHours(-1).AddMinutes(-1), now.AddHours(-1).AddMinutes(1));
        Assert.Equal("no_published_date", rows.Single(row => row.Title == "Без даты в ленте").RejectionReason);
        Assert.Equal("empty_body", rows.Single(row => row.Title == "Пустая страница").LastError);
        Assert.DoesNotContain(rows, row => row.Title is "Старая" or "Сбой сервера");

        Assert.All(rows.Where(row => row.Status == "queued"), row => Assert.Null(row.Importance));
        await new AlfaNews.Core.Ingest.NewsProcessingSchedule(db, new JobQueue(db, TimeProvider.System), TimeProvider.System).EnqueueAsync(CancellationToken.None);
        var triageJob = await db.BackgroundJobRows.SingleAsync(row => row.Kind == "TriageNews");
        var queuedIds = rows.Where(row => row.Status == "queued").Select(row => row.Id.ToString()).ToHashSet();
        var parseJobs = await db.BackgroundJobRows.AsNoTracking().Where(row => row.Kind == "ParseNews").ToListAsync();
        Assert.DoesNotContain(parseJobs, job => queuedIds.Contains(job.Payload.GetProperty("news_raw_id").GetString()!));
        Assert.True(queuedIds.SetEquals(triageJob.Payload.GetProperty("news_raw_ids").EnumerateArray().Select(value => value.GetString()!)));

        var source = await db.SourceRows.AsNoTracking().SingleAsync(row => row.Id == ids[0]);
        Assert.NotNull(source.LastSuccessAt);
        Assert.Null(source.LastError);

        var again = (await service.CollectAsync([ids[0]], CancellationToken.None)).Single();
        Assert.Equal(0, again.Collected);
        Assert.Equal(4, await db.NewsRawRows.CountAsync(row => row.SourceId == ids[0]));
    }

    [PostgresFact]
    public async Task CatalogLoaderEnablesOnlyCheckedEnabledSources()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sources_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            [
              {"id":"cat_on","name":"A","feed_url":"https://a.example/rss","kind":"media","rubric":"economy","publisher":"A","allowed_hosts":["a.example"],"enabled":true,"poll_seconds":120,"reliability":0.9,"validation_status":"checked","checked_at":null,"selection_reason":"r"},
              {"id":"cat_off","name":"B","feed_url":"https://b.example/rss","kind":"official","rubric":"government","publisher":"B","allowed_hosts":["b.example"],"enabled":false,"poll_seconds":120,"reliability":1,"validation_status":"checked","checked_at":"2026-10-21T21:19:00+03:00","selection_reason":"r"},
              {"id":"cat_rejected","name":"C","feed_url":"https://c.example/rss","kind":"official","rubric":"government","publisher":"C","allowed_hosts":["c.example"],"enabled":true,"poll_seconds":120,"reliability":1,"validation_status":"rejected","checked_at":null,"selection_reason":"r"}
            ]
            """);
        try
        {
            await using var db = fixture.CreateDb();
            var loader = new SourceCatalogLoader(db);
            Assert.Equal(3, await loader.LoadAsync(path, CancellationToken.None));
            Assert.Equal(3, await loader.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }

        await using var check = fixture.CreateDb();
        var rows = await check.SourceRows.AsNoTracking().Where(row => row.Id.StartsWith("cat_")).ToDictionaryAsync(row => row.Id);
        Assert.Equal(3, rows.Count);
        Assert.True(rows["cat_on"].Enabled);
        Assert.False(rows["cat_off"].Enabled);
        Assert.False(rows["cat_rejected"].Enabled);
    }
}