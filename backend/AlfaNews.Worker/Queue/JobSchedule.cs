using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Queue;

public sealed class JobSchedule(AlfaNewsDbContext db, JobQueue queue, TimeProvider timeProvider, IConfiguration configuration)
{
    public async Task EnqueueAsync(CancellationToken cancellationToken)
    {
        await EnqueueCollectionAsync(cancellationToken);
        await EnqueueFeedRefreshAsync(cancellationToken);
    }

    public async Task EnqueueCollectionAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var enabled = await db.SourceRows.AsNoTracking().Where(source => source.Enabled)
            .Select(source => new { source.Id, source.PollSeconds, source.LastCheckedAt }).ToListAsync(cancellationToken);
        var collectionKind = JobKinds.Name<CollectNewsJob>();
        var activePayloads = await db.BackgroundJobRows.AsNoTracking().Where(row => row.Kind == collectionKind
                && row.OwnerUserId == null && row.CompanyId == null && (row.Status == "queued" || row.Status == "running"))
            .Select(row => row.Payload).ToListAsync(cancellationToken);
        var busy = activePayloads.SelectMany(payload => payload.Deserialize<CollectNewsJob>(JobKinds.JsonOptions)!.SourceIds)
            .ToHashSet(StringComparer.Ordinal);
        var due = enabled.Where(source => !busy.Contains(source.Id)
            && (source.LastCheckedAt is null || source.LastCheckedAt.Value.AddSeconds(source.PollSeconds) <= now));
        foreach (var source in due.OrderBy(source => source.PollSeconds).ThenBy(source => source.Id, StringComparer.Ordinal))
            await queue.EnqueueAsync(new CollectNewsJob([source.Id]), null, null, cancellationToken);
    }

    public async Task EnqueueFeedRefreshAsync(CancellationToken cancellationToken)
    {
        var companyIds = (configuration["Feed:CompanyIds"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (companyIds.Length == 0)
            return;
        var companies = await db.CompanyRows.AsNoTracking()
            .Where(company => company.ClType == "CLIENT" && companyIds.Contains(company.Id))
            .OrderBy(company => company.Id).Select(company => company.Id).ToArrayAsync(cancellationToken);
        var refreshKind = JobKinds.Name<RefreshFeedJob>();
        var activeCompanies = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == refreshKind && row.OwnerUserId == null && (row.Status == "queued" || row.Status == "running"))
            .Select(row => row.CompanyId).ToArrayAsync(cancellationToken);
        var active = activeCompanies.ToHashSet(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();
        foreach (var company in companies)
            if (!active.Contains(company))
                await queue.EnqueueAsync(new RefreshFeedJob(company, now), null, company, cancellationToken);
    }
}