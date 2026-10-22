using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Llm;
using AlfaNews.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlfaNews.Tests;

public sealed class LlmGatewayDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task CacheHitUsesNoHttpAndInvalidatesOnInputVersion()
    {
        await ResetAsync();
        var transport = new ScriptedClient(ValidNews(), ValidNews());
        await using var services = Services(transport);
        var request = LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId);
        var first = await GenerateAsync<NewsExtraction>(services, request);
        var cached = await GenerateAsync<NewsExtraction>(services, request);
        var changed = await GenerateAsync<NewsExtraction>(services, request with { InputVersion = "v2" });
        Assert.False(first.CacheHit);
        Assert.True(cached.CacheHit);
        Assert.False(changed.CacheHit);
        Assert.Equal(2, transport.Calls);
        await using var db = fixture.CreateDb();
        var hit = await db.LlmUsageRows.SingleAsync(row => row.CacheHit);
        Assert.Equal(0m, hit.ActualCostRub);
        Assert.Equal(0, hit.Attempt);
    }

    [PostgresFact]
    public async Task ThinkingModeHasAnIndependentCacheEntry()
    {
        await ResetAsync();
        var options = Options();
        var transport = new ScriptedClient(ValidNews(), ValidNews());
        await using var services = Services(transport, options);
        var request = LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId);
        var thinking = await GenerateAsync<NewsExtraction>(services, request);
        options.DeepSeekThinkingEnabled = false;
        var direct = await GenerateAsync<NewsExtraction>(services, request);
        var cached = await GenerateAsync<NewsExtraction>(services, request);
        Assert.NotEqual(thinking.CacheKey, direct.CacheKey);
        Assert.False(direct.CacheHit);
        Assert.True(cached.CacheHit);
        Assert.Equal(2, transport.Calls);
    }

    [PostgresFact]
    public async Task ConcurrentReservationsCannotSpendTheSameRemainderAndSurviveNewServices()
    {
        await ResetAsync();
        var options = Options();
        options.DailyLimitRub = 40;
        var transport = new ScriptedClient(new LlmClientResponse(Error: "llm_timeout", ActualCostRub: null));
        await using (var services = Services(transport, options))
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
            var run = new AlfaNews.Core.Data.Entities.AgentRunEntity
            {
                Id = Guid.NewGuid(),
                JobId = fixture.JobId,
                InputVersion = "v1",
                Status = "running",
                StartedAt = DateTimeOffset.UtcNow
            };
            db.AgentRunRows.Add(run);
            await db.SaveChangesAsync();
            var request = LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId);
            var budget = services.GetRequiredService<LlmBudget>();
            var route = options.Route(request.Task, request.Task);
            var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                try
                {
                    await budget.StartAsync(run.Id, request, request.Task, route, "reserve-" + index, 1, 30, CancellationToken.None);
                    return "reserved";
                }
                catch (LlmException exception)
                {
                    return exception.Code;
                }
            }));
            Assert.Contains("reserved", attempts);
            Assert.Contains("budget_exhausted", attempts);
        }
        await using var restarted = Services(transport, options);
        var failure = await Assert.ThrowsAsync<LlmException>(() => GenerateAsync<NewsExtraction>(restarted,
            LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId)));
        Assert.Equal("budget_exhausted", failure.Code);
        Assert.Equal(0, transport.Calls);
    }

    [PostgresFact]
    public async Task CriticTimeoutIsNotRetriedAfterRestartOrManualRepair()
    {
        await ResetAsync();
        var client = new ScriptedClient(new LlmClientResponse(Error: "llm_timeout", Transient: true));
        var request = LlmPromptTests.Request(LlmTask.Critic, fixture.JobId) with { CardId = fixture.InsightId };
        await using (var services = Services(client))
        {
            var failure = await Assert.ThrowsAsync<LlmException>(() => GenerateAsync<CriticResult>(services, request));
            Assert.Equal("llm_timeout", failure.Code);
        }
        await using (var restarted = Services(client))
        {
            var failure = await Assert.ThrowsAsync<LlmException>(() => GenerateAsync<CriticResult>(restarted, request));
            Assert.Equal("critic_already_attempted", failure.Code);
            await using var scope = restarted.CreateAsyncScope();
            await Assert.ThrowsAsync<LlmException>(() => scope.ServiceProvider.GetRequiredService<LlmGateway>()
                .RepairAsync<CriticResult>(request, "{}", ["invalid"], CancellationToken.None));
        }
        Assert.Equal(1, client.Calls);
        await using var db = fixture.CreateDb();
        var physical = await db.LlmUsageRows.SingleAsync(row => row.Attempt > 0);
        Assert.Null(physical.ActualCostRub);
        Assert.True(physical.ReservedCostRub > 0);
    }

    [PostgresFact]
    public async Task AllowsOneSharedRepairAndKeepsChatCap()
    {
        await ResetAsync();
        var client = new ScriptedClient(new LlmClientResponse("malformed", ActualCostRub: 0.1m),
            new LlmClientResponse("""{"claims":[],"suggested_questions":[]}""", ActualCostRub: 0.2m));
        await using var services = Services(client);
        var request = LlmPromptTests.Request(LlmTask.Chat, fixture.JobId);
        await GenerateAsync<ChatDraft>(services, request);
        Assert.Equal(new[] { 8000, 8000 }, client.Limits);
        await using var scope = services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<LlmException>(() => scope.ServiceProvider.GetRequiredService<LlmGateway>()
            .RepairAsync<ChatDraft>(request, "different", ["semantic"], CancellationToken.None));
        Assert.Equal("repair_already_used", error.Code);
        Assert.Equal(2, client.Calls);
    }

    [PostgresFact]
    public async Task ThreeTransientFailuresAndOneRepairNeverExceedFourHttpCalls()
    {
        await ResetAsync();
        var client = new ScriptedClient(new LlmClientResponse(Error: "llm_rate_limited", Transient: true, ActualCostRub: 0),
            new LlmClientResponse(Error: "llm_unavailable", Transient: true, ActualCostRub: 0),
            new LlmClientResponse("malformed", ActualCostRub: 0.1m), ValidNews());
        await using var services = Services(client);
        var request = LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId);
        await GenerateAsync<NewsExtraction>(services, request);
        Assert.Equal(4, client.Calls);
        var hit = await GenerateAsync<NewsExtraction>(services, request);
        Assert.True(hit.CacheHit);
        Assert.Equal(4, client.Calls);
    }

    [PostgresFact]
    public async Task MissingKeyIsVisibleWithoutNetworkAndDoesNotConsumeCriticAllowance()
    {
        await ResetAsync();
        var client = new ScriptedClient(new LlmClientResponse("""{"issues":[]}""", ActualCostRub: 0.1m));
        var options = Options();
        options.ApiKey = "";
        var request = LlmPromptTests.Request(LlmTask.Critic, fixture.JobId);
        await using (var services = Services(client, options))
        {
            var error = await Assert.ThrowsAsync<LlmException>(() => GenerateAsync<CriticResult>(services, request));
            Assert.Equal("llm_key_required", error.Code);
        }
        await using (var services = Services(client))
            await GenerateAsync<CriticResult>(services, request);
        Assert.Equal(1, client.Calls);
    }

    private async Task ResetAsync()
    {
        await using var db = fixture.CreateDb();
        await db.LlmUsageRows.ExecuteDeleteAsync();
        await db.LlmCacheRows.ExecuteDeleteAsync();
        await db.AgentRunRows.ExecuteDeleteAsync();
    }

    [PostgresFact]
    public async Task SharedAgentRunKeepsAllStagesTogetherAndLeavesLifecycleToCaller()
    {
        await ResetAsync();
        var client = new ScriptedClient(new LlmClientResponse("""{"calls":[],"questions_to_resolve":[]}""", ActualCostRub: 0.1m),
            new LlmClientResponse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "fixtures", "InsightDraft.json")), ActualCostRub: 0.1m));
        await using var services = Services(client);
        var runId = Guid.NewGuid();
        await using (var db = fixture.CreateDb())
        {
            db.AgentRunRows.Add(new AlfaNews.Core.Data.Entities.AgentRunEntity
            {
                Id = runId,
                JobId = fixture.JobId,
                InputVersion = "synthetic-v1",
                Status = "running",
                StartedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await GenerateAsync<AgentPlan>(services, LlmPromptTests.Request(LlmTask.Plan, fixture.JobId) with { RunId = runId });
        await GenerateAsync<InsightDraft>(services, LlmPromptTests.Request(LlmTask.Draft, fixture.JobId) with { RunId = runId });
        await using var check = fixture.CreateDb();
        Assert.Equal(2, await check.LlmUsageRows.CountAsync(row => row.RunId == runId));
        Assert.Equal("running", (await check.AgentRunRows.SingleAsync(row => row.Id == runId)).Status);
    }

    [PostgresFact]
    public async Task EvaluationCreatesValidJobAndWritesAReplayReport()
    {
        await ResetAsync();
        var options = Options();
        options.UseMock = true;
        await using var services = Services(new MockLlmClient(), options);
        var directory = Path.Combine(Path.GetTempPath(), "alfanews_evaluate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "cases.json");
        var output = Path.Combine(directory, "report.json");
        try
        {
            var suite = new LlmEvaluation.EvaluationSuite("synthetic", [new LlmEvaluation.EvaluationCase("news", "NewsExtract",
                LlmPromptTests.Request(LlmTask.NewsExtract).Values)]);
            await File.WriteAllTextAsync(input, System.Text.Json.JsonSerializer.Serialize(suite));
            for (var index = 0; index < 2; index++)
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<LlmEvaluation>().RunAsync(input, output, CancellationToken.None);
            }
            var report = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await File.ReadAllTextAsync(output));
            Assert.True(report.GetProperty("use_mock").GetBoolean());
            Assert.True(report.GetProperty("cases")[0].GetProperty("cache_hit").GetBoolean());
            Assert.Equal(0m, report.GetProperty("known_cost_rub").GetDecimal());
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
            Directory.Delete(directory);
        }
    }

    [PostgresFact]
    public async Task RequestSpanningThreeMoscowKeepsItsReserveInTheNewDay()
    {
        await ResetAsync();
        var clock = new TestClock { Now = DateTimeOffset.Parse("2026-10-22T02:59:59+03:00").ToUniversalTime() };
        var options = Options();
        options.DailyLimitRub = 40;
        await using var services = Services(new ScriptedClient(), options, clock);
        var runId = Guid.NewGuid();
        await using (var db = fixture.CreateDb())
        {
            db.AgentRunRows.Add(new AlfaNews.Core.Data.Entities.AgentRunEntity
            {
                Id = runId,
                JobId = fixture.JobId,
                InputVersion = "v1",
                Status = "running",
                StartedAt = clock.Now
            });
            await db.SaveChangesAsync();
        }
        var budget = services.GetRequiredService<LlmBudget>();
        var request = LlmPromptTests.Request(LlmTask.NewsExtract, fixture.JobId);
        var route = options.Route(request.Task, request.Task);
        var first = await budget.StartAsync(runId, request, request.Task, route, "before-reset", 1, 30, CancellationToken.None);
        clock.Now = clock.Now.AddSeconds(3);
        var pendingError = await Assert.ThrowsAsync<LlmException>(() => budget.StartAsync(runId, request,
            request.Task, route, "after-reset-pending", 1, 20, CancellationToken.None));
        Assert.Equal("budget_exhausted", pendingError.Code);
        await budget.FinishAsync(first, new LlmClientResponse(Error: "llm_timeout"), 3000, null, CancellationToken.None);
        var finishedError = await Assert.ThrowsAsync<LlmException>(() => budget.StartAsync(runId, request,
            request.Task, route, "after-reset-finished", 1, 20, CancellationToken.None));
        Assert.Equal("budget_exhausted", finishedError.Code);
    }

    private ServiceProvider Services(ILlmClient client, LlmOptions? options = null, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddAlfaNewsData(fixture.ConnectionString!);
        services.AddAlfaNewsLlm(options ?? Options());
        services.AddSingleton(client);
        services.AddScoped<LlmEvaluation>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static LlmOptions Options() => new() { UseMock = false, ApiKey = "synthetic", BaseUrl = "https://example.org/v1" };

    private static async Task<LlmResult<T>> GenerateAsync<T>(ServiceProvider services, LlmRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LlmGateway>().GenerateAsync<T>(request, CancellationToken.None);
    }

    private static LlmClientResponse ValidNews() => new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Prompts", "fixtures", "NewsExtraction.json")), ActualCostRub: 0.1m);

    private sealed class ScriptedClient(params LlmClientResponse[] responses) : ILlmClient
    {
        public int Calls { get; private set; }
        public List<int> Limits { get; } = [];

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken)
        {
            Limits.Add(route.MaxOutputTokens);
            return Task.FromResult(responses[Calls++]);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}