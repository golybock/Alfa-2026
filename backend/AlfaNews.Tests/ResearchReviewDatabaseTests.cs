using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class ResearchReviewDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly ChartDraft Chart = new("G1", "Ставка", "Ставка по графику, приблизительно", null, null, "%",
        "2026",
        ["Ставка"], [new ChartPointDraft("Ставка", "2026", 14, null, null, "%", true, "axis_estimate")],
        [0.1m, 0.1m, 0.5m, 0.5m]);

    [PostgresFact]
    public async Task ReviewChecksVersionAndBlocksThenReindexesOnlyChangedContent()
    {
        var (first, second) = await SeedChunksAsync();
        await using var db = fixture.CreateDb();
        var review = new ResearchReview(db, new JobQueue(db, TimeProvider.System), TimeProvider.System);
        var ok = Chunk(first, "Исправленный текст о ставке.", ReviewStatus.Reviewed,
            new ChartReviewRequest(Chart, ReviewStatus.Reviewed));

        Assert.Equal("stale_review",
            (await Fail(() => review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "\"old\"", new([ok]),
                CancellationToken.None))).Code);
        Assert.Equal("stale_review",
            (await Fail(() =>
                review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, null, new([ok]), CancellationToken.None)))
            .Code);
        Assert.Equal(400, (await Fail(() => review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "v1",
            new([ok with { ChunkId = Guid.NewGuid().ToString() }]), CancellationToken.None))).StatusCode);
        Assert.Equal(400, (await Fail(() => review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "v1",
            new([ok with { Charts = [new ChartReviewRequest(Chart with { LocalId = "G9" }, ReviewStatus.Reviewed)] }]),
            CancellationToken.None))).StatusCode);
        Assert.Equal(400, (await Fail(() => review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "v1",
            new([ok with { Charts = [new ChartReviewRequest(Chart, ReviewStatus.NotReviewed)] }]),
            CancellationToken.None))).StatusCode);
        db.ChangeTracker.Clear();

        var result = await review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "W/\"v1\"", new([ok]),
            CancellationToken.None);
        Assert.Equal([first], result.ChangedChunkIds);
        Assert.NotEqual("v1", result.Response.Version);
        await using (var read = fixture.CreateDb())
        {
            var chunk = await read.ResearchChunkRows.AsNoTracking().SingleAsync(row => row.Id == first);
            Assert.Equal(("Исправленный текст о ставке.", "reviewed", null),
                (chunk.Text, chunk.ReviewStatus, chunk.Embedding));
            Assert.NotEqual("c1", chunk.Version);
            Assert.Equal(ReviewStatus.Reviewed,
                chunk.ChartData.Deserialize<ReviewableChartDto[]>(JobKinds.JsonOptions)![0].ReviewStatus);
            Assert.Equal("partially_reviewed",
                (await read.ResearchPageRows.AsNoTracking()
                    .SingleAsync(row => row.DocumentId == fixture.DocumentId && row.PageNo == 1)).ReviewStatus);
            Assert.Equal("partially_reviewed",
                (await read.ResearchDocRows.AsNoTracking().SingleAsync(row => row.Id == fixture.DocumentId))
                .ReviewStatus);
            var embed = JsonSerializer.SerializeToElement(
                new EmbedContentJob("research_chunk", first.ToString(), chunk.Version), JobKinds.JsonOptions);
            Assert.True(await read.BackgroundJobRows.AnyAsync(row =>
                row.Kind == "EmbedContent" && row.Payload.Equals(embed)));
            Assert.Equal("FinalizePdf",
                (await read.BackgroundJobRows.SingleAsync(row => row.Id == Guid.Parse(result.Response.JobId))).Kind);
        }

        db.ChangeTracker.Clear();
        var untouched = Chunk(second, "Второй блок.", ReviewStatus.Reviewed);
        Assert.Equal("stale_review",
            (await Fail(() => review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, "v1", new([untouched]),
                CancellationToken.None))).Code);
        db.ChangeTracker.Clear();
        var statusOnly = await review.ApplyAsync(fixture.Operator, fixture.DocumentId, 1, result.Response.Version,
            new([untouched]), CancellationToken.None);
        Assert.Empty(statusOnly.ChangedChunkIds);
        await using (var read = fixture.CreateDb())
        {
            Assert.Equal("reviewed",
                (await read.ResearchPageRows.AsNoTracking()
                    .SingleAsync(row => row.DocumentId == fixture.DocumentId && row.PageNo == 1)).ReviewStatus);
            Assert.Equal("partially_reviewed",
                (await read.ResearchDocRows.AsNoTracking().SingleAsync(row => row.Id == fixture.DocumentId))
                .ReviewStatus);
            Assert.Equal("c2",
                (await read.ResearchChunkRows.AsNoTracking().SingleAsync(row => row.Id == second)).Version);
        }
    }

    private static ChunkReviewRequest Chunk(Guid id, string text, ReviewStatus status,
        params ChartReviewRequest[] charts) =>
        new(id.ToString(), text, status, [], charts, null);

    private static async Task<AccessException> Fail(Func<Task> action) =>
        await Assert.ThrowsAsync<AccessException>(action);

    private async Task<(Guid First, Guid Second)> SeedChunksAsync()
    {
        await using var db = fixture.CreateDb();
        var pageId = await db.ResearchPageRows.Where(row => row.DocumentId == fixture.DocumentId && row.PageNo == 1)
            .Select(row => row.Id).SingleAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        const string empty = "{}";
        var charts = JsonSerializer.Serialize(new[] { new ReviewableChartDto(Chart, ReviewStatus.NotReviewed) },
            JobKinds.JsonOptions);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       INSERT INTO research_chunk (id, page_id, local_id, kind, text, chart_data, tags_json, review_status, version, search_text)
                                                       VALUES ({first}, {pageId}, 'b1', 'fact', 'Текст о ставке.', {charts}::jsonb, {empty}::jsonb, 'not_reviewed', 'c1', to_tsvector('russian', 'ставка')),
                                                              ({second}, {pageId}, 'b2', 'fact', 'Второй блок.', '[]'::jsonb, {empty}::jsonb, 'not_reviewed', 'c2', to_tsvector('russian', 'блок'))
                                                       """);
        return (first, second);
    }
}