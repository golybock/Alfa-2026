using System.Text.Json.Nodes;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class FeedInsightServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task FeedIsEmptyWithoutSnapshotAndForbiddenForForeignCompanyOrRole()
    {
        await using var db = fixture.CreateDb();
        var feed = CreateFeed(db);

        var empty = await feed.GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        Assert.Empty(empty.Items);
        Assert.Equal(RefreshStatus.Idle, empty.RefreshStatus);
        Assert.Null(empty.LastUpdatedAt);

        await AssertDeniedAsync(403, () => feed.GetAsync(fixture.User, "test_company_b", Role.Cfo, CancellationToken.None));
        await AssertDeniedAsync(403, () => feed.GetAsync(fixture.User, "test_company_a", Role.Ceo, CancellationToken.None));
        await AssertDeniedAsync(403, () => feed.RefreshAsync(fixture.User, "test_company_b", CancellationToken.None));
    }

    [PostgresFact]
    public async Task HideAppliesOnlyToTheUserWhoHidAndKeepsRankAndRoleStatus()
    {
        await using var db = fixture.CreateDb();
        var feed = CreateFeed(db);
        var eventId = await db.InsightRows.Where(row => row.Id == fixture.InsightId).Select(row => row.EventId).SingleAsync();
        await InsertSnapshotAsync(db, eventId);

        var visible = await feed.GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        var item = Assert.Single(visible.Items);
        Assert.Equal(AnalysisStatus.Ready, item.AnalysisStatus);
        Assert.Equal(fixture.InsightId.ToString(), item.InsightId);
        Assert.Equal(1, item.Relevance.Rank);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO feedback (user_id, company_id, event_id, kind, updated_at)
            VALUES ({fixture.UserId}, 'test_company_a', {eventId}, 'hide', {DateTimeOffset.UtcNow})
            """);
        Assert.Empty((await feed.GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None)).Items);
        var other = new UserContext(fixture.OtherUserId, fixture.OtherSessionId);
        Assert.Single((await feed.GetAsync(other, "test_company_a", Role.Cfo, CancellationToken.None)).Items);
    }

    [PostgresFact]
    public async Task RefreshReusesActiveJobAndJobStaysWithItsOwner()
    {
        await using var db = fixture.CreateDb();
        var feed = CreateFeed(db);
        var first = await feed.RefreshAsync(fixture.User, "test_company_a", CancellationToken.None);
        var second = await feed.RefreshAsync(fixture.User, "test_company_a", CancellationToken.None);
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal($"/api/jobs/{first.JobId}", first.StatusUrl);

        var access = new AccessService(db, TimeProvider.System);
        var job = await access.RequireJobAsync(fixture.User, Guid.Parse(first.JobId), CancellationToken.None);
        Assert.Equal(JobStatus.Queued, JobQueue.ToResponse(job, "trace").Status);
        var other = new UserContext(fixture.OtherUserId, fixture.OtherSessionId);
        await AssertDeniedAsync(403, () => access.RequireJobAsync(other, Guid.Parse(first.JobId), CancellationToken.None));
    }

    [PostgresFact]
    public async Task InsightReturnsReadyCardAndSeparatesMissingForeignAndUnknownEvent()
    {
        await using var db = fixture.CreateDb();
        var insights = CreateInsights(db);
        var eventId = (await db.InsightRows.Where(row => row.Id == fixture.InsightId).Select(row => row.EventId).SingleAsync()).ToString();

        var ready = await insights.GetAsync(fixture.User, "test_company_a", eventId, Role.Cfo, CancellationToken.None);
        Assert.Equal(fixture.InsightId.ToString(), ready.Insight?.Id);
        var ensured = await insights.EnsureAsync(fixture.User, "test_company_a", eventId, Role.Cfo, CancellationToken.None);
        Assert.Equal(fixture.InsightId.ToString(), ensured.Insight?.Id);

        await AssertDeniedAsync(403, () => insights.GetAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None));
        await AssertDeniedAsync(403, () => insights.GetAsync(fixture.User, "test_company_a", eventId, Role.Ceo, CancellationToken.None));
        await AssertDeniedAsync(404, () => insights.GetAsync(fixture.User, "test_company_a", Guid.NewGuid().ToString(), Role.Cfo, CancellationToken.None));
        await AssertDeniedAsync(400, () => insights.GetAsync(fixture.User, "test_company_a", "not-a-guid", Role.Cfo, CancellationToken.None));
    }

    [PostgresFact]
    public async Task InsightWithoutCardIsNotPreparedUntilEnsureQueuesOneJob()
    {
        await using var db = fixture.CreateDb();
        var insights = CreateInsights(db);
        var eventId = (await db.InsightRows.Where(row => row.Id == fixture.InsightId).Select(row => row.EventId).SingleAsync()).ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE insight SET status = 'failed', body_json = NULL WHERE id = {fixture.ForeignInsightId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_company_access (user_id, company_id, role) VALUES ({fixture.UserId}, 'test_company_b', 'cfo')
            """);

        var failed = await Assert.ThrowsAsync<AccessException>(() =>
            insights.GetAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None));
        Assert.Equal(409, failed.StatusCode);
        Assert.Equal("analysis_failed", failed.Code);

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM insight WHERE id = {fixture.ForeignInsightId}");
        var missing = await Assert.ThrowsAsync<AccessException>(() =>
            insights.GetAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None));
        Assert.Equal("insight_not_prepared", missing.Code);

        var first = await insights.EnsureAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None);
        var second = await insights.EnsureAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None);
        Assert.NotNull(first.Accepted);
        Assert.Equal(first.Accepted.JobId, second.Accepted?.JobId);

        var pending = await insights.GetAsync(fixture.User, "test_company_b", eventId, Role.Cfo, CancellationToken.None);
        Assert.Equal(AnalysisStatus.Queued, pending.Pending?.Status);
        Assert.Equal(first.Accepted.JobId, pending.Pending?.JobId);
    }

    private FeedService CreateFeed(Core.Data.AlfaNewsDbContext db) =>
        new(db, new AccessService(db, TimeProvider.System), new JobQueue(db, TimeProvider.System), new Core.Feed.FeedSettings(), TimeProvider.System);

    private InsightService CreateInsights(Core.Data.AlfaNewsDbContext db) =>
        new(db, new AccessService(db, TimeProvider.System), new JobQueue(db, TimeProvider.System));

    private static async Task InsertSnapshotAsync(Core.Data.AlfaNewsDbContext db, Guid eventId)
    {
        var json = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Examples", "feed-ready.json")))!;
        var items = json["items"]!;
        items[0]!["event_id"] = eventId.ToString();
        var itemsJson = items.ToJsonString();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO feed_snapshot (company_id, as_of, news_period_from, news_period_to, input_version, items_json, last_updated_at, refresh_status)
            VALUES ('test_company_a', {now}, {now.AddDays(-7)}, {now}, 'v1', {itemsJson}::jsonb, {now}, 'idle')
            """);
    }

    private static async Task AssertDeniedAsync(int status, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<AccessException>(action);
        Assert.Equal(status, exception.StatusCode);
    }
}