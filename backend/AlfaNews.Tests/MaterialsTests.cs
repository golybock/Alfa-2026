using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlfaNews.Tests;

public sealed class MaterialsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ArticleText =
        "Синтетический абзац ручной новости о решении регулятора и его последствиях для компаний отрасли. ";

    [Fact]
    public void MaterialsAreOpenToEveryActiveAccountInDemo()
    {
        Assert.True(AccessService.CanAddMaterials(new AppUserEntity { Login = "admin", IsOperator = true }));
        Assert.True(AccessService.CanAddMaterials(new AppUserEntity { Login = UserSeedService.AllCompaniesLogin }));
        Assert.True(AccessService.CanAddMaterials(new AppUserEntity { Login = "U_68157" }));
        Assert.False(AccessService.CanAddMaterials(new AppUserEntity { Login = "U_68157", Active = false }));
    }

    [Fact]
    public void DocumentWaitsForKeyOnlyWhenEveryPageFailedForMissingKey()
    {
        const string key = DocumentStatuses.KeyRequired;
        Assert.True(DocumentStatuses.WaitsForKey("failed", [("failed", key), ("failed", key)]));
        Assert.False(DocumentStatuses.WaitsForKey("failed", [("failed", key), ("failed", "pdf_page_failed")]));
        Assert.False(DocumentStatuses.WaitsForKey("partial", [("ready", null), ("failed", key)]));
        Assert.False(DocumentStatuses.WaitsForKey("failed", []));
    }

    [PostgresFact]
    public async Task ManualNewsLinkMustComeFromEnabledSourceSite()
    {
        await using var db = fixture.CreateDb();
        var id = "manual_" + Guid.NewGuid().ToString("N")[..8];
        var host = id.Replace('_', '-') + ".test";
        db.SourceRows.Add(Source(id, [host, "www." + host]));
        var user = new AppUserEntity { Id = Guid.NewGuid(), Login = id, DisplayName = id, PasswordHash = "-" };
        db.AppUserRows.Add(user);
        await db.SaveChangesAsync();
        var service = new ManualNewsService(db, new JobQueue(db, TimeProvider.System));

        Assert.Equal((id, $"https://{host}/news/1"),
            await service.ResolveAsync(Request(null, $"https://{host}/news/1"), CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<AccessException>(() =>
            service.ResolveAsync(Request(null, "https://example.org/news"), CancellationToken.None));
        Assert.Equal(400, foreign.StatusCode);
        Assert.Contains(host, foreign.Message);
        Assert.DoesNotContain("www." + host, foreign.Message);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessException>(() =>
            service.ResolveAsync(new ManualNewsRequest(id, null, "Заголовок", "Текст", DateTimeOffset.UtcNow),
                CancellationToken.None))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessException>(() =>
                service.ResolveAsync(Request("missing_source", $"https://{host}/news/1"), CancellationToken.None)))
            .StatusCode);

        var job = await service.EnqueueAsync(Request(null, $"https://{host}/news/2"), user.Id, CancellationToken.None);
        Assert.Equal("CollectManualNews", job.Kind);
        Assert.Equal(id, job.Payload.GetProperty("request").GetProperty("source_id").GetString());

        var sources = (await service.SourcesAsync(CancellationToken.None)).Items;
        Assert.Contains(sources, source => source.Id == id && !source.IsSpecialized);
        Assert.DoesNotContain(sources, source => source.Id == "fns-news");

        var list = await service.ListAsync(user.Id, "test", CancellationToken.None);
        var item = Assert.Single(list.Items);
        Assert.Equal((job.Id.ToString(), $"https://{host}/news/2", null, JobStatus.Queued),
            (item.JobId, item.Url, item.Title, item.Status));
        Assert.Empty((await service.ListAsync(Guid.NewGuid(), "test", CancellationToken.None)).Items);
    }

    [Fact]
    public void ManualNewsRequestNeedsOnlyUrlInJson()
    {
        var request =
            JsonSerializer.Deserialize<ManualNewsRequest>("""{"url":"https://www.rbc.ru/news"}""", JobKinds.JsonOptions)
            !;
        Assert.Equal(new ManualNewsRequest(Url: "https://www.rbc.ru/news"), request);
    }

    [PostgresFact]
    public async Task ManualNewsIsCollectedAsImportantAndRaisesTriagedDuplicate()
    {
        var now = DateTimeOffset.UtcNow;

        string Article(DateTimeOffset date) =>
            $"<html><body><article><h1>Ручная новость</h1><time datetime=\"{date:O}\">дата</time><p>{string.Concat(Enumerable.Repeat(ArticleText, 4))}</p></article></body></html>";

        using var server = new LocalHttpServer(path => path switch
        {
            "/fresh" or "/triaged" => new LocalResponse(200, Article(now.AddHours(-2))),
            "/old" => new LocalResponse(200, Article(now.AddDays(-20))),
            "/empty" => new LocalResponse(200, "<html><body><p>Пусто</p></body></html>"),
            _ => new LocalResponse(503, "busy")
        });
        await using var db = fixture.CreateDb();
        var id = "manual_" + Guid.NewGuid().ToString("N")[..8];
        db.SourceRows.Add(Source(id, ["127.0.0.1"]));
        db.NewsRawRows.Add(new NewsRawEntity
        {
            Id = Guid.NewGuid(),
            SourceId = id,
            CanonicalUrl = server.BaseUrl + "/triaged",
            Title = "Отсеяна фильтром заголовков",
            Text = ArticleText,
            TextSha256 = TextNormalizer.Sha256(ArticleText),
            PublishedAt = now.AddHours(-2),
            FetchedAt = now,
            Status = "rejected",
            Importance = 3,
            RejectionReason = "headline_irrelevant"
        });
        await db.SaveChangesAsync();
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);
        var service = new CollectNewsService(db, fetcher, TimeProvider.System, NullLogger<CollectNewsService>.Instance);

        Assert.Equal(new ManualCollectResult("collected"),
            await service.CollectManualAsync(id, server.BaseUrl + "/fresh?utm_source=tg", CancellationToken.None));
        var fresh = await db.NewsRawRows.AsNoTracking()
            .SingleAsync(row => row.CanonicalUrl == server.BaseUrl + "/fresh");
        Assert.Equal(("queued", 1, "Ручная новость"), (fresh.Status, fresh.Importance, fresh.Title));
        Assert.Equal(new ManualCollectResult("duplicate"),
            await service.CollectManualAsync(id, server.BaseUrl + "/fresh", CancellationToken.None));

        Assert.Equal(new ManualCollectResult("collected"),
            await service.CollectManualAsync(id, server.BaseUrl + "/triaged", CancellationToken.None));
        var triaged = await db.NewsRawRows.AsNoTracking()
            .SingleAsync(row => row.CanonicalUrl == server.BaseUrl + "/triaged");
        Assert.Equal(("queued", 1, null), (triaged.Status, triaged.Importance, triaged.RejectionReason));

        Assert.Equal("news_outside_window",
            (await service.CollectManualAsync(id, server.BaseUrl + "/old", CancellationToken.None)).ErrorCode);
        Assert.Equal("news_empty",
            (await service.CollectManualAsync(id, server.BaseUrl + "/empty", CancellationToken.None)).ErrorCode);
        Assert.Equal("news_unavailable",
            (await service.CollectManualAsync(id, server.BaseUrl + "/down", CancellationToken.None)).ErrorCode);
        Assert.Equal(2, await db.NewsRawRows.CountAsync(row => row.SourceId == id));
    }

    [PostgresFact]
    public async Task ReviewedResearchFromIndexMarksDocumentPagesChunksAndCharts()
    {
        await using var db = fixture.CreateDb();

        (ResearchDocEntity Doc, ResearchChunkEntity Chunk) Add(string status)
        {
            var doc = new ResearchDocEntity
            {
                Id = Guid.NewGuid(),
                Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                FilePath = "files/x.pdf",
                Title = "Обзор",
                PageCount = 1,
                OcrStatus = "ready",
                ReviewStatus = "not_reviewed",
                Version = "v1",
                CreatedAt = DateTimeOffset.UtcNow
            };
            var page = new ResearchPageEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                PageNo = 1,
                ImagePath = "pages/x/1.png",
                OcrStatus = status,
                ReviewStatus = "not_reviewed"
            };
            var chunk = new ResearchChunkEntity
            {
                Id = Guid.NewGuid(),
                PageId = page.Id,
                LocalId = "B1",
                Kind = "fact",
                Text = "Текст обзора.",
                ReviewStatus = "not_reviewed",
                Version = "v1",
                SearchText = NpgsqlTypes.NpgsqlTsVector.Empty,
                TagsJson = JsonSerializer.SerializeToElement(new { }),
                ChartData = JsonSerializer.SerializeToElement(new[]
                    { new { chart = new { local_id = "G1" }, review_status = "not_reviewed" } })
            };
            db.ResearchDocRows.Add(doc);
            db.ResearchPageRows.Add(page);
            db.ResearchChunkRows.Add(chunk);
            return (doc, chunk);
        }

        var listed = Add("ready");
        var other = Add("ready");
        await db.SaveChangesAsync();

        Assert.Equal(3, await ReviewedResearch.ApplyAsync(db, [listed.Doc.Sha256], CancellationToken.None));
        Assert.Equal(0, await ReviewedResearch.ApplyAsync(db, [listed.Doc.Sha256], CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal("reviewed", (await db.ResearchDocRows.SingleAsync(row => row.Id == listed.Doc.Id)).ReviewStatus);
        Assert.Equal("reviewed",
            (await db.ResearchPageRows.SingleAsync(row => row.DocumentId == listed.Doc.Id)).ReviewStatus);
        var chunk = await db.ResearchChunkRows.SingleAsync(row => row.Id == listed.Chunk.Id);
        Assert.Equal("reviewed", chunk.ReviewStatus);
        Assert.Equal("reviewed", chunk.ChartData[0].GetProperty("review_status").GetString());
        Assert.Equal("not_reviewed",
            (await db.ResearchDocRows.SingleAsync(row => row.Id == other.Doc.Id)).ReviewStatus);
    }

    private static ManualNewsRequest Request(string? sourceId, string url) => new(sourceId, url, null, null, null);

    private static SourceEntity Source(string id, string[] hosts) => new()
    {
        Id = id,
        Name = id,
        FeedUrl = $"https://{hosts[0]}/rss",
        Kind = "media",
        AllowedHosts = JsonSerializer.SerializeToElement(hosts),
        Enabled = true,
        Reliability = 0.9m,
        ValidationStatus = "checked"
    };
}