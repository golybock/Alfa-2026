using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Feed;

public sealed class FeedService(
    AlfaNewsDbContext db,
    AccessService access,
    JobQueue jobs,
    FeedSettings settings,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan NewsWindow = TimeSpan.FromDays(7);

    public async Task<FeedResponse> GetAsync(UserContext user, string companyId, Role role,
        CancellationToken cancellationToken)
    {
        await access.RequireCompanyAccessAsync(user, companyId, role, cancellationToken);
        var snapshot = await db.FeedSnapshotRows.AsNoTracking()
            .SingleOrDefaultAsync(row => row.CompanyId == companyId && row.Mode == settings.Mode, cancellationToken);
        var (lastCollectedAt, collectionFailed) = await CollectionAsync(cancellationToken);
        if (snapshot is null)
        {
            var now = timeProvider.GetUtcNow();
            return new FeedResponse(companyId, now, now - NewsWindow, now, null, null, RefreshStatus.Idle, [],
                lastCollectedAt, collectionFailed);
        }

        var hidden = await db.FeedbackRows.AsNoTracking()
            .Where(row => row.UserId == user.UserId && row.CompanyId == companyId && row.Kind == "hide")
            .Select(row => row.EventId.ToString()).ToListAsync(cancellationToken);
        var items = (snapshot.ItemsJson.Deserialize<List<FeedItemDto>>(JsonOptions)
                     ?? throw new InvalidOperationException("Сохранённая лента не содержит данных."))
            .Where(item => !hidden.Contains(item.EventId)).ToList();
        var eventIds = items.Select(item => Guid.Parse(item.EventId)).ToList();
        var roleName = AccessService.RoleName(role);
        var insights = await db.InsightRows.AsNoTracking()
            .Where(row => row.CompanyId == companyId && row.Role == roleName && eventIds.Contains(row.EventId))
            .Select(row => new { row.Id, row.EventId, row.Status, row.UpdatedAt }).ToListAsync(cancellationToken);
        var byEvent = insights.GroupBy(row => row.EventId.ToString()).ToDictionary(group => group.Key,
            group => group.OrderBy(row => row.Status == "ready" ? 0 : 1).ThenByDescending(row => row.UpdatedAt)
                .First());
        var readyIds = byEvent.Values.Where(row => row.Status == "ready").Select(row => row.Id).ToList();
        var bodies = (await db.InsightRows.AsNoTracking().Where(row => readyIds.Contains(row.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Id, AccessService.ReadReadyInsight);
        var failedIds = byEvent.Values.Where(row => row.Status == "failed").Select(row => (Guid?)row.Id).ToList();
        var runs = await db.AgentRunRows.AsNoTracking().Where(row => failedIds.Contains(row.InsightId))
            .GroupBy(row => row.InsightId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(
                row => row.Key!.Value,
                row => row.Count, cancellationToken);
        var kind = JobKinds.Name<GenerateInsightJob>();
        var jobs = (await db.BackgroundJobRows.AsNoTracking()
                .Where(row =>
                    row.Kind == kind && row.CompanyId == companyId &&
                    (row.Status == "queued" || row.Status == "running"))
                .Select(row => new { row.Payload, row.Status, row.CreatedAt }).ToListAsync(cancellationToken))
            .Select(row => (Job: row.Payload.Deserialize<GenerateInsightJob>(JobKinds.JsonOptions), row.Status,
                row.CreatedAt))
            .Where(row => row.Job is not null && row.Job.Role == role)
            .GroupBy(row => row.Job!.EventId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.CreatedAt).First());
        var forRole = items.Select(item =>
        {
            var relevance = item.Relevance with { Significance = FeedRanking.SignificanceOf(item.Relevance) };
            jobs.TryGetValue(item.EventId, out var job);
            var running = job.Status == "running";
            var queuedAt = job.Job is null ? (DateTimeOffset?)null : job.CreatedAt;
            if (!byEvent.TryGetValue(item.EventId, out var insight))
                return item with
                {
                    Relevance = relevance,
                    AnalysisStatus = running ? AnalysisStatus.Processing : AnalysisStatus.Queued,
                    InsightId = null,
                    QueuedAt = queuedAt
                };
            if (insight.Status == "ready")
            {
                var body = bodies[insight.Id];
                return item with
                {
                    Relevance = relevance,
                    AnalysisStatus = AnalysisStatus.Ready,
                    InsightId = insight.Id.ToString(),
                    InsightHeadline = body.Headline,
                    InsightWhyYou = body.WhyYou,
                    Direction = DirectionOf(body)
                };
            }

            return item with
            {
                Relevance = relevance,
                AnalysisStatus = Enum.Parse<AnalysisStatus>(insight.Status, true),
                InsightId = null,
                QueuedAt = queuedAt,
                RetryAvailable = insight.Status == "failed" &&
                                 runs.GetValueOrDefault(insight.Id) < InsightService.MaxRuns
            };
        }).ToList();
        return new FeedResponse(companyId, snapshot.AsOf, snapshot.NewsPeriodFrom, snapshot.NewsPeriodTo,
            snapshot.LastUpdatedAt, snapshot.CollectionStartedAt,
            Enum.Parse<RefreshStatus>(snapshot.RefreshStatus, true),
            forRole, lastCollectedAt, collectionFailed);
    }

    // Направление строки ленты: если все последствия карточки одного знака — он; если есть и риски, и возможности —
    // знак первого (модель ставит самое важное первым); без рисков и возможностей — нейтрально.
    public static ConsequenceType DirectionOf(InsightDto insight)
    {
        var signed = insight.Consequences.Where(item => item.Type != ConsequenceType.Neutral).Select(item => item.Type)
            .ToList();
        return signed.Count == 0 ? ConsequenceType.Neutral : signed[0];
    }

    // Сбор не удался, если у каждого включённого источника последняя проверка закончилась ошибкой.
    private async Task<(DateTimeOffset? LastCollectedAt, bool Failed)> CollectionAsync(
        CancellationToken cancellationToken)
    {
        var sources = await db.SourceRows.AsNoTracking().Where(row => row.Enabled)
            .Select(row => new { row.LastCheckedAt, row.LastSuccessAt }).ToListAsync(cancellationToken);
        var failed = sources.Count > 0 && sources.All(row => row.LastCheckedAt is { } checkedAt
                                                             && (row.LastSuccessAt is not { } success ||
                                                                 success < checkedAt));
        return (sources.Max(row => row.LastSuccessAt), failed);
    }

    public async Task<AcceptedResponse> RefreshAsync(UserContext user, string companyId,
        CancellationToken cancellationToken)
    {
        await access.RequireCompanyAsync(user, companyId, cancellationToken);
        var kind = JobKinds.Name<RefreshFeedJob>();
        var activeId = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == kind && row.CompanyId == companyId && row.OwnerUserId == user.UserId
                          && (row.Status == "queued" || row.Status == "running"))
            .OrderBy(row => row.CreatedAt).Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken);
        var jobId = activeId
                    ?? (await jobs.EnqueueAsync(new RefreshFeedJob(companyId, timeProvider.GetUtcNow()), user.UserId,
                        companyId,
                        cancellationToken)).Id;
        return new AcceptedResponse(jobId.ToString(), $"/api/jobs/{jobId}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}