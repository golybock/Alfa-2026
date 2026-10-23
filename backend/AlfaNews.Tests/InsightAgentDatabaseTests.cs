using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;

namespace AlfaNews.Tests;

public sealed class InsightAgentDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task AgentPlansCallsToolsDraftsAndRecordsPassedCritic()
    {
        var client = new AgentClient(CriticResponse: """{"issues":[]}""");
        var (insight, job) = await RunAsync(client, Role.Cfo, "test_company_a", "shared");
        Assert.Equal("ready", insight.Status);
        Assert.Null(job.ErrorCode);
        var card = AccessService.ReadReadyInsight(insight);
        Assert.Equal(CriticStatus.Passed, card.Verification.Critic.Status);
        Assert.Equal(1, card.Verification.Critic.RequestCount);
        Assert.Equal(0, card.Verification.RepairCount);
        Assert.All(card.Claims, claim => Assert.Equal(ClaimVerification.CriticPassed, claim.VerificationStatus));
        Assert.Contains(card.Evidence.Values, evidence => evidence.Type == EvidenceType.BankResearch);
        Assert.Contains(card.Evidence.Values, evidence => evidence.Type == EvidenceType.CompanyFeature && evidence.Key == "products");
        Assert.Equal(1, card.Relevance.Rank);
        Assert.Equal(2, client.Plans);
        Assert.Contains("\"role\":\"cfo\"", client.LastDraftContext);
        Assert.Contains("\"role\":null", client.LastPlanContext);
        Assert.DoesNotContain("\"bank_research\"", client.LastCriticContext);
        Assert.Contains("\"bank_research\"", client.LastDraftContext);
        await using var db = fixture.CreateDb();
        var steps = await db.AgentStepRows.AsNoTracking().Where(row => row.RunId == Guid.Parse(card.TraceId)).OrderBy(row => row.Ordinal).ToListAsync();
        Assert.Equal(["plan", "tool", "tool", "tool", "tool", "plan", "draft", "critic"], steps.Select(step => step.Stage));
        Assert.Equal("search_bank_research", steps[3].ToolName);
        Assert.Equal("search_similar_news", steps[4].ToolName);
        Assert.NotNull(steps[3].ArgumentsJson);
        Assert.NotNull(steps[3].ResultJson);
        var again = await RunAsync(new AgentClient(CriticResponse: """{"issues":[]}"""), Role.Cfo, "test_company_a", "shared");
        Assert.Equal(insight.Id, again.Insight.Id);
        var otherRole = new AgentClient(CriticResponse: """{"issues":[]}""");
        var (ceo, _) = await RunAsync(otherRole, Role.Ceo, "test_company_a", "shared");
        Assert.Equal("ready", ceo.Status);
        Assert.Equal(0, otherRole.Plans);
        Assert.Contains("\"role\":\"ceo\"", otherRole.LastDraftContext);
    }

    [PostgresFact]
    public async Task CriticIssueGetsOneRepairAndUnfixedClaimStaysUnconfirmed()
    {
        var client = new AgentClient(CriticResponse: """{"issues":[{"claim_id":"C2","code":"unsupported_claim","explanation":"Связь не обоснована."}]}""",
            RepairResponse: Draft("Компании стоит уточнить условия кредитного договора."));
        var (insight, _) = await RunAsync(client, Role.Ceo);
        var card = AccessService.ReadReadyInsight(insight);
        Assert.Equal(CriticStatus.IssuesFound, card.Verification.Critic.Status);
        Assert.Equal(1, card.Verification.RepairCount);
        Assert.Equal(ClaimVerification.CodePassed, card.Claims.Single(claim => claim.Id == "C2").VerificationStatus);
        Assert.Equal(ClaimVerification.CriticPassed, card.Claims.Single(claim => claim.Id == "C1").VerificationStatus);

        var failing = new AgentClient(CriticResponse: """{"issues":[{"claim_id":"C2","code":"unsupported_claim","explanation":"Связь не обоснована."}]}""",
            RepairResponse: "{}", CompanyKey: "test_company_b");
        var (unfixed, _) = await RunAsync(failing, Role.Ceo, "test_company_b");
        var kept = AccessService.ReadReadyInsight(unfixed);
        Assert.Equal(ClaimVerification.NotConfirmed, kept.Claims.Single(claim => claim.Id == "C2").VerificationStatus);
        Assert.DoesNotContain("C2", kept.Actions);
        Assert.Contains(kept.Limitations, item => item.Contains("не подтверждена", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task CriticTimeoutIsSkippedOnceAndInvalidDraftFailsWithoutCritic()
    {
        var timeout = new AgentClient(CriticResponse: null);
        var (insight, _) = await RunAsync(timeout, Role.Ceo, "test_company_a", "timeout");
        var card = AccessService.ReadReadyInsight(insight);
        Assert.Equal(CriticStatus.SkippedError, card.Verification.Critic.Status);
        Assert.Equal(1, card.Verification.Critic.RequestCount);
        Assert.Equal(1, timeout.Critics);

        var invalid = new AgentClient(CriticResponse: """{"issues":[]}""", DraftResponse: Draft("Ставка выросла до 99%.", "source_fact"));
        var (failed, result) = await RunAsync(invalid, Role.Cfo, "test_company_a", "invalid");
        Assert.Equal("failed", failed.Status);
        Assert.Equal("invalid_output", result.ErrorCode);
        Assert.Equal(0, invalid.Critics);
    }

    private async Task<(InsightEntity Insight, JobResult Result)> RunAsync(AgentClient client, Role role, string companyId = "test_company_a",
        string eventKey = "agent")
    {
        await using var services = Services(client);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
        var eventId = await PrepareAsync(db, companyId, eventKey);
        var queue = scope.ServiceProvider.GetRequiredService<JobQueue>();
        var inputVersion = await db.EventRows.Where(row => row.Id == eventId).Select(row => row.InputVersion).SingleAsync();
        var payload = new GenerateInsightJob(companyId, eventId.ToString(), role, inputVersion);
        var job = await queue.FindActiveAsync(payload, null, companyId, CancellationToken.None)
            ?? await queue.EnqueueAsync(payload, null, companyId, CancellationToken.None);
        var tracked = await db.BackgroundJobRows.SingleAsync(row => row.Id == job.Id);
        tracked.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<InsightAgent>().GenerateAsync(tracked, payload, CancellationToken.None);
        await using var read = fixture.CreateDb();
        return (await read.InsightRows.AsNoTracking().SingleAsync(row => row.CompanyId == companyId && row.EventId == eventId
            && row.Role == AccessService.RoleName(role)), result);
    }

    private static async Task<Guid> PrepareAsync(AlfaNewsDbContext db, string companyId, string eventKey)
    {
        await new TaxonomyCatalogLoader(db).LoadAsync(Path.Combine(AppContext.BaseDirectory, "taxonomy.json"), CancellationToken.None);
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        var empty = new PaymentExposure(0, 0, 0, new Dictionary<string, decimal>());
        if (!await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == companyId))
        {
            db.CompanyProfileRows.Add(new CompanyProfileEntity
            {
                CompanyId = companyId,
                DataVersion = "test",
                ProfileJson = JsonSerializer.SerializeToElement(new CompanyProfile(companyId, null, "41.20", "41", "Строительство",
                    null, null, null, null, null, ["loan"], [], empty, empty, "test"), options),
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var key = "agent-" + eventKey;
        var existing = await db.EventRows.Where(row => row.Headline == key).Select(row => (Guid?)row.Id).SingleOrDefaultAsync();
        if (existing is { } id)
            return id;
        var text = "Банк России сохранил ключевую ставку. " + key;
        var news = new NewsRawEntity
        {
            Id = Guid.NewGuid(),
            SourceId = "synthetic",
            CanonicalUrl = "https://example.org/agent/" + key,
            Title = key,
            Text = text,
            TextSha256 = TextNormalizer.Sha256(text),
            PublishedAt = DateTimeOffset.UtcNow.AddHours(-1),
            FetchedAt = DateTimeOffset.UtcNow,
            Status = "accepted",
            Importance = 1
        };
        var eventId = Guid.NewGuid();
        db.NewsRawRows.Add(news);
        db.EventRows.Add(new EventEntity
        {
            Id = eventId,
            NewsRawId = news.Id,
            ContentSha256 = news.TextSha256,
            Headline = key,
            PublishedAt = news.PublishedAt.Value,
            ExtractionJson = JsonSerializer.SerializeToElement(new NewsExtraction(key, null,
                [new ExtractedFact("Банк России сохранил ключевую ставку.", "Банк России сохранил ключевую ставку.")], ["Банк России"],
                new NewsTags(["key_rate"], [], [], [], [])), LlmOutputValidator.JsonOptions),
            InputVersion = "v-" + key,
            CreatedAt = news.FetchedAt,
            SearchText = NpgsqlTsVector.Empty
        });
        db.EventTagRows.Add(new EventTagEntity { EventId = eventId, Kind = "factor", Code = "key_rate" });
        await db.SaveChangesAsync();
        await new FeedRefreshService(db, new JobQueue(db, TimeProvider.System), new RankingOptions { PrecomputeInsights = false }, TimeProvider.System)
            .RefreshAsync(new RefreshFeedJob(companyId, DateTimeOffset.UtcNow), CancellationToken.None);
        return eventId;
    }

    private ServiceProvider Services(ILlmClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAlfaNewsData(fixture.ConnectionString!);
        services.AddAlfaNewsLlm(new LlmOptions { UseMock = false, ApiKey = "synthetic", BaseUrl = "https://example.org/v1" });
        services.AddSingleton(client);
        services.AddSingleton(new RankingOptions());
        services.AddSingleton(new NewsEmbeddings(new HttpClient(new DownHandler()), new ConfigurationBuilder().Build()));
        services.AddScoped<JobQueue>();
        services.AddScoped<ResearchSearch>();
        services.AddScoped<NewsSearch>();
        services.AddScoped<AgentTools>();
        services.AddScoped<FeedRefreshService>();
        services.AddScoped<InsightAgent>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static string Draft(string inference, string origin = "ai_inference") => JsonSerializer.Serialize(new
    {
        headline = "Ставка сохранена: проверить условия кредита",
        what_happened = new[] { "C1" },
        why_it_matters = new[] { "C2" },
        consequences = Array.Empty<object>(),
        actions = new[] { "C2" },
        bank_view = new { status = "not_found", claim_ids = Array.Empty<string>() },
        claims = new object[]
        {
            new { id = "C1", text = "Банк России сохранил ключевую ставку.", origin = "source_fact", refs = new[] { "E1" }, confidence = (string?)null },
            new { id = "C2", text = inference, origin, refs = new[] { "E1", "E2" }, confidence = (string?)null }
        },
        limitations = new[] { "Условия кредитного договора неизвестны." },
        suggested_questions = new[] { "Привязана ли ставка договора к ключевой?" }
    });

    private sealed record AgentClient(string? CriticResponse, string? RepairResponse = null, string? DraftResponse = null,
        string CompanyKey = "test_company_a") : ILlmClient
    {
        public int Plans { get; private set; }
        public int Critics { get; private set; }
        public string LastDraftContext { get; private set; } = "";
        public string LastPlanContext { get; private set; } = "";
        public string LastCriticContext { get; private set; } = "";

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken)
        {
            if (prompt.User.Contains("<diagnostics_json>", StringComparison.Ordinal))
                return Respond(RepairResponse ?? "{}");
            switch (prompt.SchemaName)
            {
                case "AgentPlan":
                    Plans++;
                    LastPlanContext = prompt.User;
                    return Respond(Plans == 1
                        ? """{"calls":[{"name":"get_event","arguments":{}},{"name":"get_company_profile","arguments":{}},{"name":"search_bank_research","arguments":{"query":"ключевая ставка","factors":["key_rate"],"limit":3}},{"name":"search_similar_news","arguments":{"query":"ключевая ставка","limit":3}}],"questions_to_resolve":["Есть ли позиция банка?"]}"""
                        : """{"calls":[],"questions_to_resolve":[]}""");
                case "InsightDraft":
                    LastDraftContext = prompt.User;
                    return Respond(DraftResponse ?? Draft("Сохранение ставки сохраняет текущую стоимость кредитного финансирования."));
                case "CriticResult":
                    Critics++;
                    LastCriticContext = prompt.User;
                    return Task.FromResult(CriticResponse is null
                        ? new LlmClientResponse(Error: "llm_timeout", Transient: true, ActualCostRub: 0)
                        : new LlmClientResponse(CriticResponse, ActualCostRub: 0.01m, FinishReason: "stop"));
                default:
                    throw new InvalidOperationException(prompt.SchemaName);
            }
        }

        private static Task<LlmClientResponse> Respond(string content) =>
            Task.FromResult(new LlmClientResponse(content, ActualCostRub: 0.01m, FinishReason: "stop"));
    }

    private sealed class DownHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("TEI недоступен.");
    }
}