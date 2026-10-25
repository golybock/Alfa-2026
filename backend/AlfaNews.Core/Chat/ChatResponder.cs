using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlfaNews.Core.Chat;

public sealed class ChatResponder(
    AlfaNewsDbContext db,
    LlmGateway gateway,
    TimeProvider clock,
    ILogger<ChatResponder> logger)
{
    public const string ChecksVersion = "chat-checks-v2";
    private const int HistoryTurns = 4;
    private const int MaxAnswerLength = 1200;
    private const int MaxClaims = 6;
    private const int MaxMissing = 3;
    private const int MaxMissingLength = 200;
    private const int MaxSuggestionLength = 150;

    private static readonly string[] DefaultQuestions =
    [
        "Почему событие относится к нашей компании?", "Что стоит проверить в первую очередь?",
        "Каких данных не хватает?"
    ];

    public async Task<JobResult> AnswerAsync(BackgroundJobEntity job, AnswerChatJob payload,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(payload.ChatTurnId, out var messageId) || !Guid.TryParse(payload.InsightId,
                                                                      out var insightId)
                                                                  || !Guid.TryParse(payload.UserId, out var userId) ||
                                                                  job.OwnerUserId != userId)
            return new JobResult("rejected", ErrorCode: "invalid_payload");
        var message = await db.ChatMessageRows.SingleOrDefaultAsync(row => row.Id == messageId, cancellationToken);
        if (message is null || message.UserId != userId || message.InsightId != insightId ||
            message.Question != payload.Question)
            return new JobResult("rejected", ErrorCode: "invalid_payload");
        if (message.Status == "ready")
            return Done(message);
        var insight = await db.InsightRows.AsNoTracking().SingleAsync(row => row.Id == insightId, cancellationToken);
        var allowed = await db.UserCompanyAccessRows.AsNoTracking().AnyAsync(row => row.UserId == userId
            && row.CompanyId == insight.CompanyId && row.Role == insight.Role, cancellationToken);
        if (!allowed || insight.Status != "ready" || insight.BodyJson is null)
            return await FailAsync(message, null, allowed ? "insight_not_prepared" : "forbidden_company");
        var card = AccessService.ReadReadyInsight(insight);
        var now = clock.GetUtcNow();
        message.Status = "processing";
        var run = new AgentRunEntity
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            InsightId = insight.Id,
            InputVersion = insight.InputVersion,
            Status = "running",
            StartedAt = now
        };
        db.AgentRunRows.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var answer = await BuildAsync(job, message, insight, card, run.Id, cancellationToken);
            message.AnswerJson = JsonSerializer.SerializeToElement(answer, JobKinds.JsonOptions);
            message.Status = "ready";
            run.Status = "done";
            run.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            return Done(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var code = exception is LlmException llm ? llm.Code : "chat_failed";
            logger.LogWarning("Вопрос {MessageId}, задание {JobId}: {ErrorCode}, {ErrorType}.", message.Id, job.Id,
                code, exception.GetType().Name);
            return await FailAsync(message, run, code);
        }
    }

    private async Task<ChatAnswerDto> BuildAsync(BackgroundJobEntity job, ChatMessageEntity message,
        InsightEntity insight, InsightDto card,
        Guid runId, CancellationToken cancellationToken)
    {
        var history = await db.ChatMessageRows.AsNoTracking()
            .Where(row => row.UserId == message.UserId && row.InsightId == message.InsightId && row.Status == "ready"
                          && row.CreatedAt < message.CreatedAt)
            .OrderByDescending(row => row.CreatedAt).Take(HistoryTurns).ToListAsync(cancellationToken);
        var historyJson = JsonSerializer.SerializeToElement(history.OrderBy(row => row.CreatedAt).Select(row =>
        {
            var previous = row.AnswerJson!.Value.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
            return new
            {
                question = row.Question,
                status = previous.Status,
                answer = previous.Answer,
                claims = previous.Claims.Select(claim => claim.Text).ToArray(),
                refusal_reason = previous.RefusalReason
            };
        }), AgentTools.JsonOptions);
        var questionJson = JsonSerializer.SerializeToElement(message.Question);
        var remaining = Math.Max(0, ChatService.MaxQuestions - await ChatService.Counted(db.ChatMessageRows
                .AsNoTracking()
                .Where(row => row.UserId == message.UserId && row.InsightId == message.InsightId))
            .CountAsync(cancellationToken));

        // Гейт получает только суть карточки: доказательства ему не нужны, а вход дешевле.
        var gateContext = JsonSerializer.SerializeToElement(new
        {
            company_id = card.CompanyId,
            role = card.Role,
            headline = card.Headline,
            why_you = card.WhyYou,
            claims = card.Claims.Select(claim => claim.Text).ToArray(),
            suggested_questions = card.SuggestedQuestions
        }, AgentTools.JsonOptions);
        var gate = (await gateway.GenerateAsync<ScopeGateResult>(new LlmRequest(job.Id, $"chat:{message.Id}:scope",
            insight.InputVersion,
            LlmTask.ScopeGate, new Dictionary<string, JsonElement>
            {
                ["CONTEXT_JSON"] = gateContext,
                ["HISTORY_JSON"] = historyJson,
                ["QUESTION_JSON"] = questionJson
            }, insight.Id, RunId: runId), cancellationToken)).Value;
        if (gate.Label != ScopeLabel.InScope)
            return Refusal(message, gate.Label == ScopeLabel.Injection
                    ? "Не могу выполнить эту просьбу: она пытается изменить правила работы ассистента."
                    : "Я отвечаю только на вопросы об этом событии и о том, что оно значит для вашей компании.",
                Suggestions([], card, message.Question), remaining);

        var context = JsonSerializer.SerializeToElement(new
        {
            as_of = card.AsOf,
            role = card.Role,
            company_id = card.CompanyId,
            card = new
            {
                headline = card.Headline,
                why_you = card.WhyYou,
                what_happened = card.WhatHappened,
                why_it_matters = card.WhyItMatters,
                consequences = card.Consequences,
                actions = card.Actions,
                bank_view = card.BankView,
                claims = card.Claims,
                forecast = card.Forecast
            },
            evidence = AgentTools.ForContext(card.Evidence, null),
            limitations = card.Limitations
        }, AgentTools.JsonOptions);
        var draft = (await gateway.GenerateAsync<ChatDraft>(new LlmRequest(job.Id, $"chat:{message.Id}:answer",
            insight.InputVersion,
            LlmTask.Chat, new Dictionary<string, JsonElement>
            {
                ["CONTEXT_JSON"] = context,
                ["HISTORY_JSON"] = historyJson,
                ["QUESTION_JSON"] = questionJson
            }, insight.Id, ValidateOutput: json => Validate(json, card.Evidence), ValidationVersion: ChecksVersion,
            RunId: runId), cancellationToken)).Value;
        var suggestions = Suggestions(draft.SuggestedQuestions, card, message.Question);
        var missing = draft.Missing.Select(item => item.Trim())
            .Where(item => item.Length is > 0 and <= MaxMissingLength)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxMissing).ToArray();
        var text = string.IsNullOrWhiteSpace(draft.Answer) ? null : draft.Answer.Trim();
        if (text is null && draft.Claims.Count == 0)
            return Refusal(message, "В материалах карточки нет данных, чтобы ответить на этот вопрос.", suggestions,
                remaining, missing);
        var claims = draft.Claims.Take(MaxClaims).ToArray();
        var refs = claims.SelectMany(claim => claim.Refs).ToHashSet(StringComparer.Ordinal);
        return new ChatAnswerDto(message.Id.ToString(), message.InsightId.ToString(), ChatAnswerStatus.Answer,
            claims.Select(claim => new ClaimDto(claim.Id, claim.Text, claim.Origin, claim.Refs,
                ClaimVerification.CodePassed,
                claim.Confidence)).ToArray(),
            card.Evidence.Where(pair => refs.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            null, suggestions, remaining, text, missing);
    }

    // Утверждения проверяются как в карточке. Связный ответ пересказывает утверждения: число в нём должно быть
    // в утверждениях или в процитированных доказательствах, иначе модель могла его придумать.
    public static IReadOnlyList<string> Validate(JsonElement output, IReadOnlyDictionary<string, EvidenceDto> evidence)
    {
        var errors = InsightChecks.ValidateClaims(output, evidence).ToList();
        var claims = output.GetProperty("claims").EnumerateArray().ToArray();
        var answer = output.GetProperty("answer").GetString() ?? "";
        if (answer.Length > MaxAnswerLength)
            errors.Add("answer:too_long");
        var supported = claims.SelectMany(claim => InsightChecks.Numbers(claim.GetProperty("text").GetString() ?? ""))
            .Concat(claims.SelectMany(claim => claim.GetProperty("refs").EnumerateArray())
                .Select(item => item.GetString()!)
                .Where(evidence.ContainsKey).SelectMany(item => InsightChecks.EvidenceNumbers(evidence[item])))
            .ToHashSet(StringComparer.Ordinal);
        if (InsightChecks.Numbers(answer).Any(number => !supported.Contains(number)))
            errors.Add("answer:number_not_in_evidence");
        return errors;
    }

    private static ChatAnswerDto Refusal(ChatMessageEntity message, string reason, IReadOnlyList<string> suggestions,
        int remaining,
        IReadOnlyList<string>? missing = null) =>
        new(message.Id.ToString(), message.InsightId.ToString(), ChatAnswerStatus.Refusal, [],
            new Dictionary<string, EvidenceDto>(),
            reason, suggestions, remaining, null, missing ?? []);

    private static string[] Suggestions(IEnumerable<string> preferred, InsightDto card, string question) =>
        preferred.Concat(card.SuggestedQuestions).Concat(DefaultQuestions)
            .Select(item => item.Trim())
            .Where(item => item.Length is > 0 and <= MaxSuggestionLength
                           && !string.Equals(item, question.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();

    private async Task<JobResult> FailAsync(ChatMessageEntity message, AgentRunEntity? run, string code)
    {
        message.Status = "failed";
        if (run is not null)
        {
            run.Status = "failed";
            run.FinishedAt = clock.GetUtcNow();
        }

        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await db.SaveChangesAsync(cleanup.Token);
        return new JobResult("failed", ErrorCode: code);
    }

    private static JobResult Done(ChatMessageEntity message) => new("done", "chat", message.Id.ToString());
}