using System.Text.Json;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using AlfaNews.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AlfaNews.Tests;

public sealed class SourcePollingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [PostgresFact]
    public async Task EachSourceIsPolledByItsOwnIntervalInSeparateJobs()
    {
        await using var db = fixture.CreateDb();
        await db.BackgroundJobRows.Where(row => row.Kind == "CollectNews").ExecuteDeleteAsync();
        await db.SourceRows.Where(row => row.Id.StartsWith("poll_")).ExecuteDeleteAsync();
        db.SourceRows.AddRange(NewSource("poll_fast_a", 120), NewSource("poll_fast_b", 120), NewSource("poll_slow", 900));
        await db.SaveChangesAsync();
        await db.SourceRows.Where(row => row.Id != "synthetic" && !row.Id.StartsWith("poll_")).ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, false));
        await db.SourceRows.Where(row => row.Id == "synthetic").ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, false));

        var clock = new FixedTime(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
        var schedule = new JobSchedule(db, new JobQueue(db, clock), clock, new ConfigurationBuilder().Build());

        await schedule.EnqueueCollectionAsync(CancellationToken.None);
        Assert.Equal([["poll_fast_a", "poll_fast_b"], ["poll_slow"]], await QueuedGroupsAsync(db));

        await schedule.EnqueueCollectionAsync(CancellationToken.None);
        Assert.Equal(2, await db.BackgroundJobRows.CountAsync(row => row.Kind == "CollectNews"));

        await db.BackgroundJobRows.Where(row => row.Kind == "CollectNews").ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "done"));
        await db.SourceRows.Where(row => row.Id.StartsWith("poll_")).ExecuteUpdateAsync(update => update.SetProperty(row => row.LastCheckedAt, clock.Now));

        clock.Now = clock.Now.AddMinutes(1);
        await schedule.EnqueueCollectionAsync(CancellationToken.None);
        Assert.Equal(2, await db.BackgroundJobRows.CountAsync(row => row.Kind == "CollectNews"));

        clock.Now = clock.Now.AddMinutes(2);
        await schedule.EnqueueCollectionAsync(CancellationToken.None);
        Assert.Equal(3, await db.BackgroundJobRows.CountAsync(row => row.Kind == "CollectNews"));
        Assert.Equal(["poll_fast_a", "poll_fast_b"], (await QueuedGroupsAsync(db)).Single());

        await db.BackgroundJobRows.Where(row => row.Kind == "CollectNews").ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "done"));
        await db.SourceRows.Where(row => row.Id.StartsWith("poll_fast")).ExecuteUpdateAsync(update => update.SetProperty(row => row.LastCheckedAt, clock.Now));
        clock.Now = clock.Now.AddMinutes(12);
        await schedule.EnqueueCollectionAsync(CancellationToken.None);
        Assert.Contains(["poll_slow"], await QueuedGroupsAsync(db), new SequenceComparer());
    }

    private static SourceEntity NewSource(string id, int pollSeconds) => new()
    {
        Id = id,
        Name = id,
        FeedUrl = "https://" + id + ".example/rss",
        Kind = "media",
        AllowedHosts = JsonSerializer.SerializeToElement(new[] { id + ".example" }),
        Enabled = true,
        PollSeconds = pollSeconds,
        Reliability = 0.9m,
        ValidationStatus = "checked"
    };

    private static async Task<List<string[]>> QueuedGroupsAsync(AlfaNews.Core.Data.AlfaNewsDbContext db)
    {
        var payloads = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == "CollectNews" && row.Status == "queued").OrderBy(row => row.CreatedAt).Select(row => row.Payload).ToListAsync();
        return payloads.Select(payload => payload.GetProperty("source_ids").EnumerateArray().Select(id => id.GetString()!).ToArray()).ToList();
    }

    private sealed class SequenceComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y) => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(string[] obj) => obj.Length;
    }
}