using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Activity;

public sealed class UserActivityService(AlfaNewsDbContext db, AccessService access, TimeProvider timeProvider)
{
    public async Task RecordAsync(UserContext user, Guid insightId, UserActivityRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) || eventId == Guid.Empty)
            throw InvalidInput();
        var insight = await access.RequireInsightAsync(user, insightId, cancellationToken);
        var body = AccessService.ReadReadyInsight(insight);
        var kind = request.Type switch
        {
            UserActivityType.InsightOpened => "insight_opened",
            UserActivityType.ReasonsOpened => "reasons_opened",
            UserActivityType.SourceOpened => "source_opened",
            UserActivityType.ActionInteracted => "action_interacted",
            _ => throw InvalidInput()
        };
        if ((request.Type == UserActivityType.SourceOpened) != (request.EvidenceId is not null)
            || (request.Type == UserActivityType.ActionInteracted) != (request.ActionId is not null))
            throw InvalidInput();
        if (request.EvidenceId is not null && !body.Evidence.ContainsKey(request.EvidenceId))
            throw InvalidInput();
        if (request.ActionId is not null && !body.Actions.Contains(request.ActionId))
            throw InvalidInput();
        await InsertAsync(eventId, user.UserId, insight.CompanyId, insightId, kind, request.EvidenceId,
            request.ActionId, cancellationToken);
    }

    public async Task RecordAcceptedChatAsync(UserContext user, Guid messageId, CancellationToken cancellationToken)
    {
        await access.RequireUserAsync(user, cancellationToken);
        var message = await db.ChatMessageRows.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == messageId && row.UserId == user.UserId, cancellationToken)
            ?? throw new AccessException(404, "not_found", "Принятый вопрос не найден.");
        var insight = await access.RequireInsightAsync(user, message.InsightId, cancellationToken);
        await InsertAsync(message.Id, user.UserId, insight.CompanyId, insight.Id,
            "chat_question_accepted", null, null, cancellationToken);
    }

    private async Task InsertAsync(Guid eventId, Guid userId, string companyId, Guid insightId, string kind,
        string? evidenceId, string? actionId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_activity (id, created_at, user_id, company_id, insight_id, kind, evidence_id, action_id)
            VALUES ({eventId}, {now}, {userId}, {companyId}, {insightId}, {kind}, {evidenceId}, {actionId})
            ON CONFLICT (id) DO NOTHING
            """, cancellationToken);
        if (affected != 0)
            return;
        var existing = await db.UserActivityRows.AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);
        if (existing.UserId != userId || existing.CompanyId != companyId || existing.InsightId != insightId
            || existing.Kind != kind || existing.EvidenceId != evidenceId || existing.ActionId != actionId)
            throw new AccessException(409, "event_conflict", "ID события уже использован.");
    }

    private static AccessException InvalidInput() => new(400, "invalid_input", "Некорректное событие действия.");
}