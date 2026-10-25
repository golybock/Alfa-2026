using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Chat;

public sealed record ChatAskResult(ChatAnswerDto? Answer, AcceptedResponse? Accepted);

public sealed class ChatService(
    AlfaNewsDbContext db,
    AccessService access,
    JobQueue jobs,
    UserActivityService activities,
    TimeProvider clock)
{
    public const int MaxQuestions = 10;

    public const int MaxQuestionLength = 1000;

    // Сбой не засчитывается в лимит вопросов, но каждая попытка тратит запросы модели: общее число попыток ограничено.
    public const int MaxAttempts = 2 * MaxQuestions;

    public async Task<ChatAskResult> AskAsync(UserContext user, Guid insightId, ChatRequest request,
        CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length is 0 or > MaxQuestionLength)
            throw new AccessException(400, "invalid_input", "Вопрос должен содержать от 1 до 1000 символов.");
        var insight = await access.RequireInsightAsync(user, insightId, cancellationToken);
        AccessService.ReadReadyInsight(insight);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"chat:{user.UserId}:{insightId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var messages = await db.ChatMessageRows.AsNoTracking()
            .Where(row => row.UserId == user.UserId && row.InsightId == insightId).ToListAsync(cancellationToken);
        var remaining = Math.Max(0, MaxQuestions - Counted(messages.AsQueryable()).Count());
        var cached = messages.Where(row => row.Status == "ready" && row.Question == question)
            .OrderByDescending(row => row.CreatedAt).FirstOrDefault();
        if (cached is not null)
            return new ChatAskResult(ReadAnswer(cached, remaining), null);
        var active = messages.FirstOrDefault(row => row.Status is "queued" or "processing");
        if (active is not null)
        {
            if (active.Question != question)
                throw new AccessException(429, "rate_limited", "Предыдущий вопрос ещё обрабатывается.");
            var existing = await LatestJobAsync(Payload(active), user.UserId, cancellationToken);
            if (existing is not null)
                return new ChatAskResult(null, Accepted(existing));
        }

        if (remaining == 0 || messages.Count >= MaxAttempts)
            throw new AccessException(429, "question_limit", "По этой карточке задано максимальное число вопросов.");
        var message = new ChatMessageEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            InsightId = insightId,
            Question = question,
            Status = "queued",
            CreatedAt = clock.GetUtcNow()
        };
        db.ChatMessageRows.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        await activities.RecordAcceptedChatAsync(user, message.Id, cancellationToken);
        var job = await jobs.EnqueueAsync(Payload(message), user.UserId, insight.CompanyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ChatAskResult(null, Accepted(job));
    }

    public async Task<ChatHistoryResponse> HistoryAsync(UserContext user, Guid insightId,
        CancellationToken cancellationToken)
    {
        var messages = await access.ReadChatHistoryAsync(user, insightId, cancellationToken);
        var remaining = Math.Max(0, MaxQuestions - Counted(messages.AsQueryable()).Count());
        // Сбой, после которого тот же вопрос задан снова, из истории убирается: повтор его заменяет.
        return new ChatHistoryResponse(messages.Where(row => row.Status is "ready" or "failed")
            .Where(row =>
                row.Status != "failed" ||
                !messages.Any(later => later.Question == row.Question && later.CreatedAt > row.CreatedAt))
            .Select(row => new ChatTurnDto(row.Id.ToString(), row.Question, row.CreatedAt, ReadAnswer(row, remaining)))
            .ToArray());
    }

    public static IQueryable<ChatMessageEntity> Counted(IQueryable<ChatMessageEntity> messages) =>
        messages.Where(row => row.Status != "failed");

    public static AnswerChatJob Payload(ChatMessageEntity message) =>
        new(message.UserId.ToString(), message.InsightId.ToString(), message.Question, message.Id.ToString());

    // Ответ читается из сохранённого JSON; остаток вопросов всегда считается на момент чтения.
    // В ответах до chat-checks-v2 нет связного текста и списка недостающих данных.
    private static ChatAnswerDto ReadAnswer(ChatMessageEntity message, int remaining)
    {
        if (message is not { Status: "ready", AnswerJson: { } json })
            return new ChatAnswerDto(message.Id.ToString(), message.InsightId.ToString(), ChatAnswerStatus.Failed, [],
                new Dictionary<string, EvidenceDto>(),
                "Не удалось подготовить ответ. Попробуйте задать вопрос ещё раз.", [], remaining,
                Missing: []);
        var answer = json.Deserialize<ChatAnswerDto>(JobKinds.JsonOptions)!;
        return answer with { Missing = answer.Missing ?? [], QuestionsRemaining = remaining };
    }

    private Task<BackgroundJobEntity?> LatestJobAsync(AnswerChatJob payload, Guid userId,
        CancellationToken cancellationToken)
    {
        var kind = JobKinds.Name<AnswerChatJob>();
        var json = JsonSerializer.SerializeToElement(payload, JobKinds.JsonOptions);
        return db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == kind && row.OwnerUserId == userId && row.Payload.Equals(json))
            .OrderByDescending(row => row.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private static AcceptedResponse Accepted(BackgroundJobEntity job) => new(job.Id.ToString(), $"/api/jobs/{job.Id}");
}