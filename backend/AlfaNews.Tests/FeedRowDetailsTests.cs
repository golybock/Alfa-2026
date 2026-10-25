using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class FeedRowDetailsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task ReadyRowShowsCardHeadlineDirectionSignificanceAndCollectionFailure()
    {
        await using var db = fixture.CreateDb();
        var eventId = await db.InsightRows.Where(row => row.Id == fixture.InsightId).Select(row => row.EventId)
            .SingleAsync();
        await InsertSnapshotAsync(db, "test_company_a", eventId);
        var card = AccessService.ReadReadyInsight(await db.InsightRows.SingleAsync(row => row.Id == fixture.InsightId));

        var feed = await CreateFeed(db).GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        var item = Assert.Single(feed.Items);
        Assert.Equal(card.Headline, item.InsightHeadline);
        Assert.NotEqual(item.Headline, item.InsightHeadline);
        Assert.Equal(FeedService.DirectionOf(card), item.Direction);
        Assert.Equal(FeedRanking.SignificanceOf(item.Relevance), item.Relevance.Significance);
        Assert.NotNull(card.Relevance.Significance);
        Assert.Null(item.QueuedAt);
        Assert.False(item.RetryAvailable);
        Assert.False(feed.CollectionFailed);

        var checkedAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       UPDATE source SET enabled = true, last_checked_at = {checkedAt}, last_success_at = {checkedAt.AddHours(-3)}, last_error = 'http_503'
                                                       """);
        var failing = await CreateFeed(db).GetAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        Assert.True(failing.CollectionFailed);
        Assert.Equal(checkedAt.AddHours(-3).ToUnixTimeSeconds(), failing.LastCollectedAt?.ToUnixTimeSeconds());
    }

    [PostgresFact]
    public async Task FailedCardIsRetriedTwiceThenStaysFailed()
    {
        await using var db = fixture.CreateDb();
        var eventId = await db.InsightRows.Where(row => row.Id == fixture.ForeignInsightId).Select(row => row.EventId)
            .SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       UPDATE insight SET status = 'failed', body_json = NULL WHERE id = {fixture.ForeignInsightId};
                                                       INSERT INTO user_company_access (user_id, company_id, role) VALUES ({fixture.UserId}, 'test_company_b', 'cfo');
                                                       """);
        await InsertSnapshotAsync(db, "test_company_b", eventId);
        var feed = CreateFeed(db);
        var insights = new InsightService(db, new AccessService(db, TimeProvider.System),
            new JobQueue(db, TimeProvider.System));

        var row = Assert.Single((await feed.GetAsync(fixture.User, "test_company_b", Role.Cfo, CancellationToken.None))
            .Items);
        Assert.Equal(AnalysisStatus.Failed, row.AnalysisStatus);
        Assert.True(row.RetryAvailable);

        var retry = await insights.EnsureAsync(fixture.User, "test_company_b", eventId.ToString(), Role.Cfo,
            CancellationToken.None);
        Assert.NotNull(retry.Accepted);
        Assert.Equal("queued",
            await db.InsightRows.Where(item => item.Id == fixture.ForeignInsightId).Select(item => item.Status)
                .SingleAsync());
        var queued =
            Assert.Single((await feed.GetAsync(fixture.User, "test_company_b", Role.Cfo, CancellationToken.None))
                .Items);
        Assert.Equal(AnalysisStatus.Queued, queued.AnalysisStatus);
        Assert.NotNull(queued.QueuedAt);

        for (var attempt = 0; attempt < InsightService.MaxRuns; attempt++)
            db.AgentRunRows.Add(new AgentRunEntity
            {
                Id = Guid.NewGuid(),
                JobId = Guid.Parse(retry.Accepted.JobId),
                InsightId = fixture.ForeignInsightId,
                InputVersion = "v1",
                Status = "failed",
                StartedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       UPDATE insight SET status = 'failed' WHERE id = {fixture.ForeignInsightId};
                                                       UPDATE background_job SET status = 'failed' WHERE id = {Guid.Parse(retry.Accepted.JobId)};
                                                       """);
        Assert.False(Assert
            .Single((await feed.GetAsync(fixture.User, "test_company_b", Role.Cfo, CancellationToken.None)).Items)
            .RetryAvailable);
        var exhausted = await Assert.ThrowsAsync<AccessException>(() =>
            insights.EnsureAsync(fixture.User, "test_company_b", eventId.ToString(), Role.Cfo, CancellationToken.None));
        Assert.Equal("analysis_failed", exhausted.Code);
    }

    [PostgresFact]
    public async Task ManagerContactIsRecordedAsActivity()
    {
        await using var db = fixture.CreateDb();
        var activities = new UserActivityService(db, new AccessService(db, TimeProvider.System), TimeProvider.System);
        var request = new UserActivityRequest(Guid.NewGuid().ToString(), UserActivityType.ManagerContactRequested, null,
            null);
        await activities.RecordAsync(fixture.User, fixture.InsightId, request, CancellationToken.None);
        Assert.Equal("manager_contact_requested",
            await db.UserActivityRows.Where(row => row.Id == Guid.Parse(request.EventId)).Select(row => row.Kind)
                .SingleAsync());
    }

    [Fact]
    public void StepsGetHumanTitlesAndHideErrorCodes()
    {
        AgentStepEntity Step(string stage, string? tool, string summary, params string[] refs) => new()
        {
            Stage = stage,
            ToolName = tool,
            Summary = summary,
            EvidenceRefs = JsonSerializer.SerializeToElement(refs),
            DurationMs = 10
        };

        var similar = AgentStepView.ToDto(Step("tool", AgentTools.SimilarNews, "Найдены похожие новости.", "E5", "E6"));
        Assert.Equal("Нашли похожие новости: 2", similar.Title);
        var critic = AgentStepView.ToDto(Step("critic", null,
            "Недостаточно дневного бюджета модели для проверки критиком. Код: budget_exhausted."));
        Assert.Equal(AgentStepStatus.Skipped, critic.Status);
        Assert.DoesNotContain("budget_exhausted", critic.Summary);
        Assert.Equal(AgentStepStatus.Failed,
            AgentStepView.ToDto(Step("repair", null,
                "Починка не прошла: invalid_output. Сохранена последняя корректная версия.")).Status);
        Assert.Equal(AgentStepStatus.Done,
            AgentStepView.ToDto(Step("draft", null, "Черновик карточки прошёл проверки кодом.")).Status);
    }

    private static FeedService CreateFeed(AlfaNewsDbContext db) =>
        new(db, new AccessService(db, TimeProvider.System), new JobQueue(db, TimeProvider.System), new FeedSettings(),
            TimeProvider.System);

    private static async Task InsertSnapshotAsync(AlfaNewsDbContext db, string companyId, Guid eventId)
    {
        var json = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Examples", "feed-ready.json")))!;
        var items = json["items"]!;
        items[0]!["event_id"] = eventId.ToString();
        var itemsJson = items.ToJsonString();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       INSERT INTO feed_snapshot (company_id, as_of, news_period_from, news_period_to, input_version, items_json, last_updated_at, refresh_status)
                                                       VALUES ({companyId}, {now}, {now.AddDays(-7)}, {now}, 'v1', {itemsJson}::jsonb, {now}, 'idle')
                                                       """);
    }
}