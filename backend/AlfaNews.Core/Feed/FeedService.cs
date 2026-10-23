using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Feed;

public sealed class FeedService(AlfaNewsDbContext db, AccessService access, JobQueue jobs, FeedSettings settings, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan NewsWindow = TimeSpan.FromDays(7);

    public async Task<FeedResponse> GetAsync(UserContext user, string companyId, Role role,
        CancellationToken cancellationToken)
    {
        await access.RequireCompanyAccessAsync(user, companyId, role, cancellationToken);
        var snapshot = await db.FeedSnapshotRows.AsNoTracking()
            .SingleOrDefaultAsync(row => row.CompanyId == companyId && row.Mode == settings.Mode, cancellationToken);
        if (snapshot is null)
        {
            var now = timeProvider.GetUtcNow();
            return new FeedResponse(companyId, now, now - NewsWindow, now, null, null, RefreshStatus.Idle, []);
        }

        var hidden = await db.FeedbackRows.AsNoTracking()
            .Where(row => row.UserId == user.UserId && row.CompanyId == companyId && row.Kind == "hide")
            .Select(row => row.EventId.ToString()).ToListAsync(cancellationToken);
        var items = (snapshot.ItemsJson.Deserialize<List<FeedItemDto>>(JsonOptions)
            ?? throw new InvalidOperationException("Сохранённая лента не содержит данных."))
            .Where(item => !hidden.Contains(item.EventId)).ToList();
        var eventIds = items.Select(item => Guid.Parse(item.EventId)).ToList();
        var insights = await db.InsightRows.AsNoTracking()
            .Where(row => row.CompanyId == companyId && row.Role == AccessService.RoleName(role) && eventIds.Contains(row.EventId))
            .Select(row => new { row.Id, row.EventId, row.Status, row.UpdatedAt }).ToListAsync(cancellationToken);
        var byEvent = insights.GroupBy(row => row.EventId.ToString()).ToDictionary(group => group.Key,
            group => group.OrderBy(row => row.Status == "ready" ? 0 : 1).ThenByDescending(row => row.UpdatedAt).First());
        var forRole = items.Select(item => byEvent.TryGetValue(item.EventId, out var insight)
            ? item with
            {
                AnalysisStatus = Enum.Parse<AnalysisStatus>(insight.Status, true),
                InsightId = insight.Status == "ready" ? insight.Id.ToString() : null
            }
            : item with { AnalysisStatus = AnalysisStatus.Queued, InsightId = null }).ToList();
        return new FeedResponse(companyId, snapshot.AsOf, snapshot.NewsPeriodFrom, snapshot.NewsPeriodTo,
            snapshot.LastUpdatedAt, snapshot.CollectionStartedAt, Enum.Parse<RefreshStatus>(snapshot.RefreshStatus, true),
            forRole);
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
            ?? (await jobs.EnqueueAsync(new RefreshFeedJob(companyId, timeProvider.GetUtcNow()), user.UserId, companyId,
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