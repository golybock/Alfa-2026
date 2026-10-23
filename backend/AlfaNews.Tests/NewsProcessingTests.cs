using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlfaNews.Tests;

public sealed class NewsProcessingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("[{\"id\":\"a\",\"importance\":\"yes\"},{\"id\":\"b\",\"importance\":\"maybe\"}]", true)]
    [InlineData("[{\"id\":\"a\",\"importance\":\"yes\"},{\"id\":\"a\",\"importance\":\"no\"}]", false)]
    [InlineData("[{\"id\":\"a\",\"importance\":\"yes\"}]", false)]
    [InlineData("[{\"id\":\"a\",\"importance\":\"yes\"},{\"id\":\"c\",\"importance\":\"no\"}]", false)]
    public void TriageRequiresEveryIdExactlyOnce(string decisions, bool valid)
    {
        var request = TriageRequest();
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var errors = LlmOutputValidator.Validate<NewsTriage>("{\"decisions\":" + decisions + "}", prompt, request, out _);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void TriageAndItsRepairDisableDeepSeekThinking()
    {
        var options = new LlmOptions();
        Assert.False(options.Route(LlmTask.NewsTriage, LlmTask.NewsTriage).ThinkingEnabled);
        Assert.False(options.Route(LlmTask.Repair, LlmTask.NewsTriage).ThinkingEnabled);
        Assert.True(options.Route(LlmTask.NewsExtract, LlmTask.NewsExtract).ThinkingEnabled);
        Assert.Equal(6000, options.Route(LlmTask.Repair, LlmTask.NewsTriage).MaxOutputTokens);
        options.TriageUseGlm = true;
        Assert.Equal(options.GlmModel, options.Route(LlmTask.Repair, LlmTask.NewsTriage).Model);
    }

    [PostgresFact]
    public async Task TriageStoresAllThreeLabelsAndNeverSendsArticleBodies()
    {
        await using var db = fixture.CreateDb();
        var rows = new[] { NewRow(Guid.NewGuid().ToString(), null), NewRow(Guid.NewGuid().ToString(), null), NewRow(Guid.NewGuid().ToString(), null) };
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index].Title = new[] { "yes", "maybe", "no" }[index];
            rows[index].Text = "article_body_must_not_be_sent_to_triage";
        }
        db.NewsRawRows.AddRange(rows);
        await db.SaveChangesAsync();
        var options = new LlmOptions();
        var client = new TriageClient();
        using var services = new ServiceCollection().AddAlfaNewsData(fixture.ConnectionString!).BuildServiceProvider();
        using var budget = new LlmBudget(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, options);
        var gateway = new LlmGateway(db, options, new LlmPromptCatalog(), client, budget, TimeProvider.System, NullLogger<LlmGateway>.Instance);
        var queue = new JobQueue(db, TimeProvider.System);
        var schedule = new NewsProcessingSchedule(db, queue, TimeProvider.System);
        var triage = new NewsTriageService(db, gateway, schedule);
        var payload = new TriageNewsJob(rows.Select(row => row.Id.ToString()).ToArray());
        var job = await queue.EnqueueAsync(payload, null, null, CancellationToken.None);
        await triage.ProcessAsync(job, payload, CancellationToken.None);
        await triage.ProcessAsync(job, payload, CancellationToken.None);
        Assert.Equal(1, client.Calls);
        Assert.Equal(new int?[] { 1, 2, 3 }, rows.Select(row => row.Importance).ToArray());
        Assert.Equal("rejected", rows[2].Status);
        Assert.Equal("headline_irrelevant", rows[2].RejectionReason);
        var scheduled = await db.BackgroundJobRows.Where(row => row.Kind == "ParseNews" && row.Status == "queued").ToArrayAsync();
        Assert.DoesNotContain(scheduled, item => item.Payload.GetProperty("news_raw_id").GetString() == rows[2].Id.ToString());
        Assert.Contains(scheduled, item => item.Payload.GetProperty("news_raw_id").GetString() == rows[0].Id.ToString() && item.Priority == 83);
        Assert.Contains(scheduled, item => item.Payload.GetProperty("news_raw_id").GetString() == rows[1].Id.ToString() && item.Priority == 79);
    }

    [PostgresFact]
    public async Task AdmissionFillsFiftySlotsWithImportantThenOldestMaybe()
    {
        await using var db = fixture.CreateDb();
        await db.BackgroundJobRows.Where(row => row.Kind == "ParseNews").ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "done"));
        await db.NewsRawRows.Where(row => row.Status == "queued").ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "rejected"));
        var prefix = Guid.NewGuid().ToString();
        var rows = Enumerable.Range(0, 55).Select(index => NewRow(prefix + index, 2)).ToArray();
        for (var index = 0; index < rows.Length; index++)
            rows[index].FetchedAt = DateTimeOffset.UtcNow.AddMinutes(-100 + index);
        var important = Enumerable.Range(0, 8).Select(index => NewRow(prefix + "important" + index, 1)).ToArray();
        var trash = NewRow(prefix + "trash", 3);
        db.NewsRawRows.AddRange(rows.Concat(important).Append(trash));
        await db.SaveChangesAsync();
        var schedule = new NewsProcessingSchedule(db, new JobQueue(db, TimeProvider.System), TimeProvider.System);
        await schedule.EnqueueAsync(CancellationToken.None);
        await schedule.EnqueueAsync(CancellationToken.None);
        var jobs = await db.BackgroundJobRows.Where(row => row.Kind == "ParseNews" && row.Status == "queued").ToArrayAsync();
        Assert.Equal(50, jobs.Length);
        var selected = jobs.Select(job => Guid.Parse(job.Payload.GetProperty("news_raw_id").GetString()!)).ToHashSet();
        Assert.All(important, row => Assert.Contains(row.Id, selected));
        Assert.All(rows.OrderBy(row => row.FetchedAt).ThenBy(row => row.Id).Take(42), row => Assert.Contains(row.Id, selected));
        Assert.DoesNotContain(trash.Id, selected);
        await db.BackgroundJobRows.Where(row => row.Kind == "ParseNews" && row.Status == "queued").ExecuteDeleteAsync();
        await db.NewsRawRows.Where(row => row.CanonicalUrl.StartsWith("https://example.org/" + prefix)).ExecuteDeleteAsync();
    }

    [PostgresFact]
    public async Task ExtractionDeduplicatesExactTextButKeepsNewNumbersAndPersistsSearch()
    {
        await using var db = fixture.CreateDb();
        await db.SourceRows.Where(row => row.Id == "synthetic").ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, true));
        if (!await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == "test_company_a"))
        {
            db.CompanyProfileRows.Add(new CompanyProfileEntity { CompanyId = "test_company_a", DataVersion = "test", ProfileJson = JsonSerializer.SerializeToElement(new { }), UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var prefix = Guid.NewGuid().ToString();
        var first = NewRow(prefix + "first", 1);
        first.Text = "Компания объявила о росте выручки на 14%. " + prefix;
        var duplicate = NewRow(prefix + "duplicate", 1);
        duplicate.Text = first.Text;
        var update = NewRow(prefix + "update", 1);
        update.Text = first.Text.Replace("14%", "16%");
        db.NewsRawRows.AddRange(first, duplicate, update);
        await db.SaveChangesAsync();
        var client = new ExtractClient();
        var options = new LlmOptions();
        using var services = new ServiceCollection().AddAlfaNewsData(fixture.ConnectionString!).BuildServiceProvider();
        using var budget = new LlmBudget(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, options);
        var gateway = new LlmGateway(db, options, new LlmPromptCatalog(), client, budget, TimeProvider.System, NullLogger<LlmGateway>.Instance);
        using var http = new HttpClient(new EmbedHandler());
        var processing = new NewsProcessing(db, gateway, new NewsEmbeddings(http, new ConfigurationBuilder().Build()), new JobQueue(db, TimeProvider.System), TimeProvider.System);
        foreach (var row in new[] { first, duplicate, update, first })
            await processing.ProcessAsync(await NewJobAsync(db), row.Id, CancellationToken.None);
        Assert.Equal(2, client.Calls);
        Assert.Equal("duplicate", duplicate.Status);
        Assert.Equal("accepted", update.Status);
        Assert.True(await db.EventSourceRows.AnyAsync(row => row.NewsRawId == duplicate.Id));
        var saved = await db.EventRows.AsNoTracking().SingleAsync(row => row.NewsRawId == first.Id);
        Assert.NotNull(saved.Embedding);
        Assert.True(await db.EventTagRows.AnyAsync(row => row.EventId == saved.Id && row.Kind == "country" && row.Code == "РОССИЯ"));
        Assert.NotEmpty(saved.SearchText.ToString());
        Assert.Equal(2, await db.EventRows.CountAsync(row => row.NewsRawId == first.Id || row.NewsRawId == update.Id));
        Assert.True(await db.BackgroundJobRows.AnyAsync(row => row.Kind == "RefreshFeed"));
    }

    [PostgresFact]
    public async Task RejectsOutsideWindowAndClearSportWithoutCallingLlm()
    {
        await using var db = fixture.CreateDb();
        await db.SourceRows.Where(row => row.Id == "synthetic").ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, true));
        var old = NewRow(Guid.NewGuid().ToString(), 1);
        old.PublishedAt = DateTimeOffset.UtcNow.AddDays(-8);
        var footer = NewRow(Guid.NewGuid().ToString(), 1);
        footer.Text = "Copyright © 2026 Interfax. Все права защищены.";
        var sport = NewRow(Guid.NewGuid().ToString(), 1);
        sport.Text = "Футбольный матч закончился со счетом 2:1.";
        db.NewsRawRows.AddRange(old, sport, footer);
        await db.SaveChangesAsync();
        var client = new ExtractClient();
        var options = new LlmOptions();
        using var services = new ServiceCollection().AddAlfaNewsData(fixture.ConnectionString!).BuildServiceProvider();
        using var budget = new LlmBudget(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, options);
        var gateway = new LlmGateway(db, options, new LlmPromptCatalog(), client, budget, TimeProvider.System, NullLogger<LlmGateway>.Instance);
        using var http = new HttpClient(new EmbedHandler(sport: true));
        var processing = new NewsProcessing(db, gateway, new NewsEmbeddings(http, new ConfigurationBuilder().Build()), new JobQueue(db, TimeProvider.System), TimeProvider.System);
        await processing.ProcessAsync(await NewJobAsync(db), old.Id, CancellationToken.None);
        await processing.ProcessAsync(await NewJobAsync(db), sport.Id, CancellationToken.None);
        await processing.ProcessAsync(await NewJobAsync(db), footer.Id, CancellationToken.None);
        Assert.Equal("boilerplate_body", footer.RejectionReason);
        Assert.Equal("outside_window", old.RejectionReason);
        Assert.Equal("e5_clearly_unrelated", sport.RejectionReason);
        Assert.Equal(0, client.Calls);
    }

    private static NewsRawEntity NewRow(string key, int? importance) => new()
    {
        Id = Guid.NewGuid(),
        SourceId = "synthetic",
        CanonicalUrl = "https://example.org/" + key,
        Title = "Новость " + key,
        Text = key,
        TextSha256 = TextNormalizer.Sha256(key),
        PublishedAt = DateTimeOffset.UtcNow.AddHours(-1),
        FetchedAt = DateTimeOffset.UtcNow,
        Importance = importance,
        Status = "queued"
    };

    private static Task<BackgroundJobEntity> NewJobAsync(AlfaNews.Core.Data.AlfaNewsDbContext db) =>
        new JobQueue(db, TimeProvider.System).EnqueueAsync(new ParseNewsJob(Guid.NewGuid().ToString()), null, null, CancellationToken.None);

    private static LlmRequest TriageRequest() => new(Guid.NewGuid(), "triage-test", "v1", LlmTask.NewsTriage,
        new Dictionary<string, JsonElement> { ["INPUT_JSON"] = JsonSerializer.SerializeToElement(new[] { new { id = "a", title = "Налоги" }, new { id = "b", title = "Матч" } }) });

    private sealed class ExtractClient : ILlmClient
    {
        public int Calls { get; private set; }

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken)
        {
            Calls++;
            var start = prompt.User.IndexOf("<input_json>", StringComparison.Ordinal) + "<input_json>".Length;
            var end = prompt.User.IndexOf("</input_json>", StringComparison.Ordinal);
            var input = JsonSerializer.Deserialize<JsonElement>(prompt.User[start..end]);
            var text = input.GetProperty("text").GetString()!;
            var result = new NewsExtraction("Рост выручки", null, [new ExtractedFact(text, text)], [], new NewsTags([], [], ["РОССИЯ"], [], []));
            return Task.FromResult(new LlmClientResponse(JsonSerializer.Serialize(result, LlmOutputValidator.JsonOptions), ActualCostRub: 0));
        }
    }

    private sealed class TriageClient : ILlmClient
    {
        public int Calls { get; private set; }

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.DoesNotContain("article_body_must_not_be_sent_to_triage", prompt.User);
            Assert.False(route.ThinkingEnabled);
            var start = prompt.User.IndexOf("<input_json>", StringComparison.Ordinal) + "<input_json>".Length;
            var end = prompt.User.IndexOf("</input_json>", StringComparison.Ordinal);
            var input = JsonSerializer.Deserialize<JsonElement>(prompt.User[start..end]);
            var decisions = input.EnumerateArray().Select(item => new { id = item.GetProperty("id").GetString(), importance = item.GetProperty("title").GetString() });
            return Task.FromResult(new LlmClientResponse(JsonSerializer.Serialize(new { decisions }), ActualCostRub: 0));
        }
    }

    private sealed class EmbedHandler(bool sport = false) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var input = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var count = input.GetProperty("inputs").GetArrayLength();
            var result = Enumerable.Range(0, count).Select(index =>
            {
                var vector = new float[768];
                vector[count == 1 ? sport ? 3 : 0 : index] = 1;
                return vector;
            }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
    }
}