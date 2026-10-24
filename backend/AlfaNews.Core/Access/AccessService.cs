using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Feed;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Access;

public sealed class AccessService(AlfaNewsDbContext db, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<AppUserEntity> RequireUserAsync(UserContext context, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await (from session in db.AuthSessionRows.AsNoTracking()
            join candidate in db.AppUserRows.AsNoTracking() on session.UserId equals candidate.Id
            where session.Id == context.SessionId && candidate.Id == context.UserId && candidate.Active
                && session.RevokedAt == null && session.ExpiresAt > now
            select candidate).SingleOrDefaultAsync(cancellationToken);
        return user ?? throw new AccessException(401, "unauthorized", "Требуется вход в систему.");
    }

    public async Task RequireCompanyAccessAsync(UserContext context, string companyId, Role role,
        CancellationToken cancellationToken)
    {
        await RequireUserAsync(context, cancellationToken);
        var roleName = RoleName(role);
        var roles = await (from access in db.UserCompanyAccessRows.AsNoTracking()
            join company in db.CompanyRows.AsNoTracking() on access.CompanyId equals company.Id
            where access.UserId == context.UserId && company.Id == companyId && company.ClType == "CLIENT"
            select access.Role).ToListAsync(cancellationToken);
        if (roles.Count == 0)
            throw new AccessException(403, "forbidden_company", "Нет доступа к компании.");
        if (!roles.Contains(roleName))
            throw new AccessException(403, "forbidden_role", "Нет доступа к выбранной роли.");
    }

    public async Task RequireCompanyAsync(UserContext context, string companyId, CancellationToken cancellationToken)
    {
        await RequireUserAsync(context, cancellationToken);
        var hasAccess = await (from access in db.UserCompanyAccessRows.AsNoTracking()
            join company in db.CompanyRows.AsNoTracking() on access.CompanyId equals company.Id
            where access.UserId == context.UserId && company.Id == companyId && company.ClType == "CLIENT"
            select access.Role).AnyAsync(cancellationToken);
        if (!hasAccess)
            throw new AccessException(403, "forbidden_company", "Нет доступа к компании.");
    }

    public async Task RequireOperatorAsync(UserContext context, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(context, cancellationToken);
        if (!user.IsOperator)
            throw new AccessException(403, "forbidden", "Требуются права оператора.");
    }

    public async Task<InsightEntity> RequireInsightAsync(UserContext context, Guid insightId,
        CancellationToken cancellationToken)
    {
        await RequireUserAsync(context, cancellationToken);
        var insight = await db.InsightRows.AsNoTracking().SingleOrDefaultAsync(row => row.Id == insightId, cancellationToken)
            ?? throw new AccessException(404, "not_found", "Карточка не найдена.");
        await RequireCompanyAccessAsync(context, insight.CompanyId, ParseRole(insight.Role), cancellationToken);
        return insight;
    }

    public async Task<EvidenceDto> RequireEvidenceAsync(UserContext context, Guid insightId, string evidenceId,
        CancellationToken cancellationToken)
    {
        var insight = await RequireInsightAsync(context, insightId, cancellationToken);
        var body = ReadReadyInsight(insight);
        return body.Evidence.TryGetValue(evidenceId, out var evidence) ? evidence
            : throw new AccessException(404, "not_found", "Источник не найден в карточке.");
    }

    public async Task<ResearchPageEntity> RequireResearchPageAsync(UserContext context, Guid documentId, int pageNo,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(context, cancellationToken);
        if (!user.IsOperator)
        {
            var candidates = await (from insight in db.InsightRows.AsNoTracking()
                join access in db.UserCompanyAccessRows.AsNoTracking()
                    on new { insight.CompanyId, insight.Role } equals new { access.CompanyId, access.Role }
                where access.UserId == context.UserId && insight.Status == "ready" && insight.BodyJson != null
                select insight).ToListAsync(cancellationToken);
            var allowed = candidates.Any(insight => ReadReadyInsight(insight).Evidence.Values.Any(evidence =>
                Guid.TryParse(evidence.DocumentId, out var referencedId) && referencedId == documentId && evidence.Page == pageNo));
            if (!allowed)
                throw new AccessException(403, "forbidden", "Нет доступа к странице документа.");
        }

        return await db.ResearchPageRows.AsNoTracking()
            .SingleOrDefaultAsync(page => page.DocumentId == documentId && page.PageNo == pageNo, cancellationToken)
            ?? throw new AccessException(404, "not_found", "Страница не найдена.");
    }

    public async Task<BackgroundJobEntity> RequireJobAsync(UserContext context, Guid jobId,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(context, cancellationToken);
        var job = await db.BackgroundJobRows.AsNoTracking().SingleOrDefaultAsync(row => row.Id == jobId, cancellationToken)
            ?? throw new AccessException(404, "not_found", "Задание не найдено.");
        if (!user.IsOperator && job.OwnerUserId != context.UserId)
            throw new AccessException(403, "forbidden", "Нет доступа к заданию.");
        return job;
    }

    public async Task<IReadOnlyList<ChatMessageEntity>> ReadChatHistoryAsync(UserContext context, Guid insightId,
        CancellationToken cancellationToken)
    {
        await RequireInsightAsync(context, insightId, cancellationToken);
        return await db.ChatMessageRows.AsNoTracking()
            .Where(message => message.UserId == context.UserId && message.InsightId == insightId)
            .OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).ToListAsync(cancellationToken);
    }

    public static InsightDto ReadReadyInsight(InsightEntity insight)
    {
        if (insight.Status != "ready" || insight.BodyJson is null)
            throw new AccessException(409, "insight_not_prepared", "Карточка ещё не готова.");
        var body = insight.BodyJson.Value.Deserialize<InsightDto>(JsonOptions)
            ?? throw new InvalidOperationException("Сохранённая карточка не содержит данных.");
        // Значимость, направление и период считаются кодом из сохранённой карточки, в том числе у карточек до появления полей.
        return body with
        {
            Relevance = body.Relevance with { Significance = FeedRanking.SignificanceOf(body.Relevance) },
            Direction = FeedService.DirectionOf(body),
            NewsPeriodFrom = body.NewsPeriodFrom ?? FeedRanking.WindowStart(body.AsOf),
            NewsPeriodTo = body.NewsPeriodTo ?? body.AsOf
        };
    }

    public static string RoleName(Role role) => role switch
    {
        Role.Ceo => "ceo",
        Role.Cfo => "cfo",
        _ => throw new AccessException(400, "invalid_input", "Неизвестная роль.")
    };

    private static Role ParseRole(string role) => role switch
    {
        "ceo" => Role.Ceo,
        "cfo" => Role.Cfo,
        _ => throw new InvalidOperationException("В карточке сохранена неизвестная роль.")
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}