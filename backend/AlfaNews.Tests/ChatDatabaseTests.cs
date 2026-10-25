using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Chat;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlfaNews.Tests;

public sealed class ChatDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Answer =
        """{"answer":"В новости указана ставка 14%.","claims":[{"id":"C1","text":"Ставка составила 14%.","origin":"source_fact","refs":["E1"],"confidence":null}],"missing":["Условия кредитов компании"],"suggested_questions":["Что будет со ставкой дальше?"]}""";

    [PostgresFact]
    public async Task InScopeQuestionGetsCheckedAnswerAndRepeatComesFromHistory()
    {
        var client = new ChatClient("in_scope", Answer);
        var insightId = await CopyInsightAsync("answer");
        var (message, result, services) = await AskAndAnswerAsync(client, insightId, "Какая ставка указана в новости?");
        await using var _ = services;
        Assert.Equal(("done", "chat", message.Id.ToString()), (result.Stage, result.ResultType, result.ResultId));
        Assert.Equal("ready", message.Status);
        var answer = message.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        Assert.Equal(ChatAnswerStatus.Answer, answer.Status);
        Assert.Equal("В новости указана ставка 14%.", answer.Answer);
        Assert.Equal(["Условия кредитов компании"], answer.Missing);
        Assert.Null(answer.RefusalReason);
        Assert.Equal(ClaimVerification.CodePassed, Assert.Single(answer.Claims).VerificationStatus);
        Assert.Equal(["E1"], answer.Evidence.Keys);
        Assert.Equal(3, answer.SuggestedQuestions.Count);
        Assert.Equal("Что будет со ставкой дальше?", answer.SuggestedQuestions[0]);
        Assert.Equal(9, answer.QuestionsRemaining);
        Assert.Equal((1, 1), (client.Gates, client.Chats));
        Assert.DoesNotContain("\"quote\"", client.LastGatePrompt);
        Assert.Contains("\"evidence\"", client.LastChatPrompt);

        await using var scope = services.CreateAsyncScope();
        var repeat = await scope.ServiceProvider.GetRequiredService<ChatService>()
            .AskAsync(fixture.User, insightId, new ChatRequest("Какая ставка указана в новости? "),
                CancellationToken.None);
        Assert.Null(repeat.Accepted);
        Assert.Equal(message.Id.ToString(), repeat.Answer!.Id);
        var history = await scope.ServiceProvider.GetRequiredService<ChatService>()
            .HistoryAsync(fixture.User, insightId, CancellationToken.None);
        Assert.Equal(ChatAnswerStatus.Answer, Assert.Single(history.Items).Answer.Status);
    }

    [PostgresFact]
    public async Task OutOfScopeAndEmptyAnswersBecomeRefusalsWithoutMainRequest()
    {
        var offTopic = new ChatClient("out_of_scope", Answer);
        var insightId = await CopyInsightAsync("refusal");
        var (message, _, services) = await AskAndAnswerAsync(offTopic, insightId, "Напиши сайт для пекарни");
        await services.DisposeAsync();
        var refusal = message.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        Assert.Equal(ChatAnswerStatus.Refusal, refusal.Status);
        Assert.Empty(refusal.Claims);
        Assert.Empty(refusal.Evidence);
        Assert.NotNull(refusal.RefusalReason);
        Assert.Equal(3, refusal.SuggestedQuestions.Count);
        Assert.Equal((1, 0), (offTopic.Gates, offTopic.Chats));

        var empty = new ChatClient("in_scope",
            """{"answer":" ","claims":[],"missing":["Маржа компании"],"suggested_questions":["Какие условия у кредита?"]}""");
        var (gap, _, more) = await AskAndAnswerAsync(empty, insightId, "Какая маржа у компании?");
        await more.DisposeAsync();
        var missing = gap.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        Assert.Equal(ChatAnswerStatus.Refusal, missing.Status);
        Assert.Contains("нет данных", missing.RefusalReason);
        Assert.Equal(["Маржа компании"], missing.Missing);
        Assert.Equal(8, missing.QuestionsRemaining);
    }

    [PostgresFact]
    public async Task GeneralAnswerNeedsNoClaimsAndInvalidClaimIsDroppedWithoutRepair()
    {
        var general = new ChatClient("in_scope",
            """{"answer":"Ключевая ставка — ориентир стоимости денег в экономике.","claims":[],"missing":[],"suggested_questions":[]}""");
        var insightId = await CopyInsightAsync("general");
        var (message, result, services) = await AskAndAnswerAsync(general, insightId, "Что такое ключевая ставка?");
        await services.DisposeAsync();
        Assert.Equal("done", result.Stage);
        var answer = message.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        Assert.Equal(ChatAnswerStatus.Answer, answer.Status);
        Assert.Empty(answer.Claims);
        Assert.Empty(answer.Evidence);

        var mixed = Answer.Replace("""],"missing""",
            """,{"id":"C2","text":"Ставка составит 15%.","origin":"source_fact","refs":["E1"],"confidence":null}],"missing""");
        var client = new ChatClient("in_scope", mixed);
        var (salvaged, keptResult, more) = await AskAndAnswerAsync(client, insightId, "Какая ставка указана?");
        await more.DisposeAsync();
        Assert.Equal(("done", null), (keptResult.Stage, keptResult.ErrorCode));
        var kept = salvaged.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        Assert.Equal("C1", Assert.Single(kept.Claims).Id);
        Assert.Equal(0, client.Repairs);
    }

    [PostgresFact]
    public async Task AnswerNumberWithoutClaimGoesToRepair()
    {
        var client = new ChatClient("in_scope",
            Answer.Replace("В новости указана ставка 14%.", "Ставка вырастет до 16%."), RepairResponse: Answer);
        var insightId = await CopyInsightAsync("answer-number");
        var (message, _, services) = await AskAndAnswerAsync(client, insightId, "Что будет со ставкой?");
        await services.DisposeAsync();
        Assert.Equal("ready", message.Status);
        Assert.Equal(1, client.Repairs);
        Assert.Equal("В новости указана ставка 14%.",
            message.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!.Answer);
    }

    [PostgresFact]
    public async Task NumberOutsideEvidenceFailsAfterOneRepairAndForeignCardIsRejected()
    {
        var client = new ChatClient("in_scope", Answer.Replace("14%", "15%"), RepairResponse: "{}");
        var insightId = await CopyInsightAsync("invalid");
        var (message, result, services) = await AskAndAnswerAsync(client, insightId, "Какая ставка?");
        await using var _ = services;
        Assert.Equal("failed", message.Status);
        Assert.Equal("invalid_output", result.ErrorCode);
        Assert.Equal(1, client.Repairs);

        await using var scope = services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<ChatService>()
            .HistoryAsync(fixture.User, insightId, CancellationToken.None);
        var failed = Assert.Single(history.Items).Answer;
        Assert.Equal(ChatAnswerStatus.Failed, failed.Status);
        Assert.Equal(10, failed.QuestionsRemaining);
        var foreign = await Assert.ThrowsAsync<AccessException>(() => scope.ServiceProvider
            .GetRequiredService<ChatService>()
            .AskAsync(fixture.User, fixture.ForeignInsightId, new ChatRequest("Почему?"), CancellationToken.None));
        Assert.Equal(403, foreign.StatusCode);
    }

    private async Task<(ChatMessageEntity Message, JobResult Result, ServiceProvider Services)> AskAndAnswerAsync(
        ChatClient client,
        Guid insightId, string question)
    {
        var services = Services(client);
        await using var scope = services.CreateAsyncScope();
        var accepted = (await scope.ServiceProvider.GetRequiredService<ChatService>()
            .AskAsync(fixture.User, insightId, new ChatRequest(question), CancellationToken.None)).Accepted!;
        var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
        var job = await db.BackgroundJobRows.SingleAsync(row => row.Id == Guid.Parse(accepted.JobId));
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var payload = job.Payload.Deserialize<AnswerChatJob>(JobKinds.JsonOptions)!;
        var result = await scope.ServiceProvider.GetRequiredService<ChatResponder>()
            .AnswerAsync(job, payload, CancellationToken.None);
        await using var read = fixture.CreateDb();
        return (await read.ChatMessageRows.AsNoTracking().SingleAsync(row => row.Id == Guid.Parse(payload.ChatTurnId)),
            result, services);
    }

    private async Task<Guid> CopyInsightAsync(string key)
    {
        await using var db = fixture.CreateDb();
        var source = await db.InsightRows.AsNoTracking().SingleAsync(row => row.Id == fixture.InsightId);
        var id = Guid.NewGuid();
        var body = JsonNode.Parse(source.BodyJson!.Value.GetRawText())!;
        body["id"] = id.ToString();
        db.InsightRows.Add(new InsightEntity
        {
            Id = id,
            CompanyId = source.CompanyId,
            EventId = source.EventId,
            Role = source.Role,
            InputVersion = "chat-" + key,
            Status = "ready",
            BodyJson = JsonSerializer.SerializeToElement(body),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    private ServiceProvider Services(ILlmClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAlfaNewsData(fixture.ConnectionString!);
        services.AddAlfaNewsLlm(new LlmOptions
            { UseMock = false, ApiKey = "synthetic", BaseUrl = "https://example.org/v1" });
        services.AddSingleton(client);
        services.AddScoped<JobQueue>();
        services.AddScoped<AccessService>();
        services.AddScoped<UserActivityService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ChatResponder>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class ChatClient(string label, string chatResponse, string? RepairResponse = null) : ILlmClient
    {
        public int Gates { get; private set; }
        public int Chats { get; private set; }
        public int Repairs { get; private set; }
        public string LastGatePrompt { get; private set; } = "";
        public string LastChatPrompt { get; private set; } = "";

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image,
            CancellationToken cancellationToken)
        {
            if (prompt.User.Contains("<diagnostics_json>", StringComparison.Ordinal))
            {
                Repairs++;
                return Respond(RepairResponse ?? "{}");
            }

            switch (prompt.SchemaName)
            {
                case "ScopeGateResult":
                    Gates++;
                    LastGatePrompt = prompt.User;
                    return Respond($$"""{"label":"{{label}}","reason":"Проверка."}""");
                case "ChatDraft":
                    Chats++;
                    LastChatPrompt = prompt.User;
                    return Respond(chatResponse);
                default:
                    throw new InvalidOperationException(prompt.SchemaName);
            }
        }

        private static Task<LlmClientResponse> Respond(string content) =>
            Task.FromResult(new LlmClientResponse(content, ActualCostRub: 0.01m, FinishReason: "stop"));
    }
}