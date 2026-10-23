using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Ingest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AlfaNews.Tests;

public sealed class ResearchSearchDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task IndexesSeededChunksAndFusesFullTextWithVectors()
    {
        await using var db = fixture.CreateDb();
        await new TaxonomyCatalogLoader(db).LoadAsync(Path.Combine(AppContext.BaseDirectory, "taxonomy.json"), CancellationToken.None);
        var target = await db.ResearchChunkRows.AsNoTracking().Where(row => row.Text.Contains("ставк")).OrderBy(row => row.Id).FirstAsync();
        using var http = new HttpClient(new VectorHandler());
        var search = new ResearchSearch(db, new NewsEmbeddings(http, new ConfigurationBuilder().Build()));
        Assert.Equal("stale", (await search.IndexAsync(new EmbedContentJob("research_chunk", target.Id.ToString(), "old"), CancellationToken.None)).Stage);
        Assert.Equal("indexed", (await search.IndexAsync(new EmbedContentJob("research_chunk", target.Id.ToString(), target.Version), CancellationToken.None)).Stage);
        Assert.NotNull((await db.ResearchChunkRows.AsNoTracking().SingleAsync(row => row.Id == target.Id)).Embedding);
        var hits = await search.SearchAsync(new SearchResearchArguments("позиция банка по ключевой ставке", ["key_rate"], 10), CancellationToken.None);
        Assert.InRange(hits.Count, 1, 6);
        Assert.Contains(hits, hit => hit.ChunkId == target.Id);
        Assert.All(hits, hit =>
        {
            Assert.False(string.IsNullOrWhiteSpace(hit.Title));
            Assert.True(hit.PageNo > 0);
            Assert.Equal(ReviewStatus.NotReviewed, hit.ReviewStatus);
            Assert.Empty(hit.ReviewedCharts);
        });
        using var down = new HttpClient(new DownHandler());
        var fullText = await new ResearchSearch(db, new NewsEmbeddings(down, new ConfigurationBuilder().Build()))
            .SearchAsync(new SearchResearchArguments("ключевая ставка", ["key_rate"], 3), CancellationToken.None);
        Assert.NotEmpty(fullText);
    }

    private sealed class VectorHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var input = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var vectors = input.GetProperty("inputs").EnumerateArray().Select(_ =>
            {
                var vector = new float[768];
                vector[0] = 1;
                return vector;
            }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(vectors) };
        }
    }

    private sealed class DownHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("TEI недоступен.");
    }
}