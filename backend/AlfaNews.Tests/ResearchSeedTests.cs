using System.Text.Json;
using AlfaNews.Core.Data.Migrations;
using AlfaNews.Core.Documents;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class ResearchSeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task MigratesRealPageResultsWithoutLlmAndPreservesUnreviewedStatus()
    {
        using var stream = typeof(SeedBankResearchOcr).Assembly.GetManifestResourceStream("BankResearchOcrSeed")!;
        using var json = await JsonDocument.ParseAsync(stream);
        var seed = json.RootElement;
        Assert.Equal(9, seed.GetProperty("documents").GetArrayLength());
        Assert.Equal(64, seed.GetProperty("pages").GetArrayLength());
        var ids = seed.GetProperty("documents").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToArray();
        await using var db = fixture.CreateDb();
        Assert.Equal(9, await db.ResearchDocRows.CountAsync(row => ids.Contains(row.Id) && row.ReviewStatus == "not_reviewed"));
        var pages = await db.ResearchPageRows.Where(row => ids.Contains(row.DocumentId)).ToArrayAsync();
        Assert.Equal(64, pages.Length);
        foreach (var page in pages)
        {
            Assert.Equal("not_reviewed", page.ReviewStatus);
            Assert.Null(page.ReviewedBy);
            Assert.Null(page.ReviewedAt);
            if (page.OcrStatus == "ready")
            {
                Assert.NotNull(page.OcrJson);
                var errors = PdfExtractionChecks.Validate(page.OcrJson.Value);
                Assert.True(errors.Count == 0, $"PDF {page.DocumentId}, страница {page.PageNo}: {string.Join(',', errors)}");
            }
            else
            {
                Assert.Equal("failed", page.OcrStatus);
                Assert.False(string.IsNullOrWhiteSpace(page.LastError));
            }
        }
        Assert.Empty(await db.LlmUsageRows.ToArrayAsync());
        var pageIds = pages.Select(row => row.Id).ToArray();
        var chunks = await db.ResearchChunkRows.Where(row => pageIds.Contains(row.PageId)).ToArrayAsync();
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal("not_reviewed", chunk.ReviewStatus);
            Assert.Null(chunk.Embedding);
            Assert.NotNull(chunk.SearchText);
            foreach (var chart in chunk.ChartData.EnumerateArray())
                Assert.Equal("not_reviewed", chart.GetProperty("review_status").GetString());
        });
    }
}