using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

// Свежие публикации разбираются первыми: после запуска лента заполняется новостями, ближайшими к текущему времени,
// а не произвольной частью недельного запаса источников.
public sealed class NewsProcessingSchedule(AlfaNewsDbContext db, JobQueue queue, TimeProvider clock)
{
    public const int BatchSize = 50;

    public async Task EnqueueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var from = await NewsWindow.StartAsync(db, now, cancellationToken);
        var to = NewsWindow.End(now);
        await db.NewsRawRows.Where(row => row.Status == "queued"
                && (row.PublishedAt == null || row.PublishedAt < from || row.PublishedAt > to))
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "rejected")
                .SetProperty(row => row.RejectionReason, row => row.PublishedAt == null ? "no_published_date" : "outside_window"), cancellationToken);
        var active = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => (row.Kind == "TriageNews" || row.Kind == "ParseNews") && (row.Status == "queued" || row.Status == "running"))
            .Select(row => new { row.Kind, row.Payload }).ToArrayAsync(cancellationToken);
        var triageBusy = active.Where(row => row.Kind == "TriageNews")
            .SelectMany(row => row.Payload.Deserialize<TriageNewsJob>(JobKinds.JsonOptions)!.NewsRawIds).ToHashSet();
        if (triageBusy.Count == 0)
        {
            var uncheckedIds = await db.NewsRawRows.AsNoTracking().Where(row => row.Status == "queued" && row.Importance == null)
                .OrderByDescending(row => row.PublishedAt).ThenBy(row => row.Id).Select(row => row.Id).Take(BatchSize).ToArrayAsync(cancellationToken);
            if (uncheckedIds.Length > 0)
                await queue.EnqueueAsync(new TriageNewsJob(uncheckedIds.Select(id => id.ToString()).ToArray()), null, null, cancellationToken);
        }
        var parseBusy = active.Where(row => row.Kind == "ParseNews")
            .Select(row => Guid.Parse(row.Payload.Deserialize<ParseNewsJob>(JobKinds.JsonOptions)!.NewsRawId)).ToArray();
        var slots = Math.Max(0, BatchSize - parseBusy.Length);
        if (slots == 0)
            return;
        var ids = await db.NewsRawRows.AsNoTracking().Where(row => row.Status == "queued"
                && (row.Importance == 1 || row.Importance == 2) && !parseBusy.Contains(row.Id))
            .OrderBy(row => row.Importance).ThenByDescending(row => row.PublishedAt).ThenBy(row => row.Id)
            .Select(row => new { row.Id, row.Importance }).Take(slots).ToArrayAsync(cancellationToken);
        foreach (var row in ids)
        {
            var job = await queue.EnqueueAsync(new ParseNewsJob(row.Id.ToString()), null, null, cancellationToken);
            job.Priority = row.Importance == 1 ? 83 : 79;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}