using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker;

public sealed class JobSchedule(AlfaNewsDbContext db, JobQueue queue, TimeProvider timeProvider, IConfiguration configuration)
{
    public async Task EnqueueAsync(CancellationToken cancellationToken)
    {
        var sources = await db.SourceRows.AsNoTracking().Where(source => source.Enabled)
            .OrderBy(source => source.Id).Select(source => source.Id).ToArrayAsync(cancellationToken);
        var collectionKind = JobKinds.Name<CollectNewsJob>();
        if (sources.Length > 0 && !await db.BackgroundJobRows.AnyAsync(row => row.Kind == collectionKind
                && row.OwnerUserId == null && row.CompanyId == null && (row.Status == "queued" || row.Status == "running"), cancellationToken))
            await queue.EnqueueAsync(new CollectNewsJob(sources), null, null, cancellationToken);
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