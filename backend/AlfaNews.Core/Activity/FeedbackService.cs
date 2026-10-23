using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Activity;

public sealed class FeedbackService(AlfaNewsDbContext db, AccessService access, TimeProvider timeProvider)
{
    // Отзыв относится к событию компании для пользователя, поэтому общий для карточек CEO и CFO; последний отзыв заменяет прежний.
    public async Task SaveAsync(UserContext user, Guid insightId, FeedbackRequest request, CancellationToken cancellationToken)
    {
        var kind = request.Kind switch
        {
            FeedbackKind.Like => "like",
            FeedbackKind.Dislike => "dislike",
            FeedbackKind.Hide => "hide",
            _ => throw InvalidInput()
        };
        var reason = request.Reason switch
        {
            null => null,
            FeedbackReason.NotRelevant => "not_relevant",
            FeedbackReason.WrongFact => "wrong_fact",
            FeedbackReason.Obvious => "obvious",
            _ => throw InvalidInput()
        };
        if (request.Kind == FeedbackKind.Like && reason is not null)
            throw InvalidInput();
        var insight = await access.RequireInsightAsync(user, insightId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO feedback (user_id, company_id, event_id, kind, reason, updated_at)
            VALUES ({user.UserId}, {insight.CompanyId}, {insight.EventId}, {kind}, {reason}, {now})
            ON CONFLICT (user_id, company_id, event_id) DO UPDATE SET kind = EXCLUDED.kind, reason = EXCLUDED.reason,
                updated_at = EXCLUDED.updated_at
            """, cancellationToken);
    }

    private static AccessException InvalidInput() => new(400, "invalid_input", "Некорректный отзыв.");
}