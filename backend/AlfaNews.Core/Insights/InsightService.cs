using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Insights;

public sealed record InsightReadResult(InsightDto? Insight, PendingInsightResponse? Pending);

public sealed record InsightEnsureResult(InsightDto? Insight, AcceptedResponse? Accepted);

public sealed class InsightService(AlfaNewsDbContext db, AccessService access, JobQueue jobs)
{
    public async Task<InsightReadResult> GetAsync(UserContext user, string companyId, string eventId, Role role,
        CancellationToken cancellationToken)
    {
        var (eventGuid, current) = await FindAsync(user, companyId, eventId, role, cancellationToken);
        if (current is null)
        {
            var queued = await FindActiveJobAsync(user, companyId, eventGuid, role, await EventInputVersionAsync(eventGuid, cancellationToken),
                cancellationToken);
            return queued is null
                ? throw new AccessException(404, "insight_not_prepared", "Карточка ещё не подготовлена.")
                : new InsightReadResult(null, new PendingInsightResponse(
                    queued.Status == "running" ? AnalysisStatus.Processing : AnalysisStatus.Queued, queued.Id.ToString()));
        }
        return current.Status switch
        {
            "ready" => new InsightReadResult(AccessService.ReadReadyInsight(current), null),
            "failed" => throw AnalysisFailed(),
            _ => new InsightReadResult(null, new PendingInsightResponse(
                Enum.Parse<AnalysisStatus>(current.Status, true),
                (await FindActiveJobAsync(user, companyId, eventGuid, role, current.InputVersion, cancellationToken))?.Id.ToString()))
        };
    }

    public async Task<InsightEnsureResult> EnsureAsync(UserContext user, string companyId, string eventId, Role role,
        CancellationToken cancellationToken)
    {
        var (eventGuid, current) = await FindAsync(user, companyId, eventId, role, cancellationToken);
        if (current?.Status == "ready")
            return new InsightEnsureResult(AccessService.ReadReadyInsight(current), null);
        if (current?.Status == "failed")
            throw AnalysisFailed();
        var inputVersion = current?.InputVersion ?? await EventInputVersionAsync(eventGuid, cancellationToken);
        var job = await jobs.EnqueueAsync(new GenerateInsightJob(companyId, eventId, role, inputVersion), user.UserId,
            companyId, cancellationToken);
        return new InsightEnsureResult(null, new AcceptedResponse(job.Id.ToString(), $"/api/jobs/{job.Id}"));
    }

    private async Task<(Guid EventId, InsightEntity? Current)> FindAsync(UserContext user, string companyId,
        string eventId, Role role, CancellationToken cancellationToken)
    {
        await access.RequireCompanyAccessAsync(user, companyId, role, cancellationToken);
        if (!Guid.TryParse(eventId, out var eventGuid))
            throw new AccessException(400, "invalid_input", "Некорректный идентификатор события.");
        if (!await db.EventRows.AsNoTracking().AnyAsync(row => row.Id == eventGuid, cancellationToken))
            throw new AccessException(404, "not_found", "Событие не найдено.");
        var roleName = AccessService.RoleName(role);
        var rows = await db.InsightRows.AsNoTracking()
            .Where(row => row.CompanyId == companyId && row.EventId == eventGuid && row.Role == roleName)
            .ToListAsync(cancellationToken);
        var current = rows.OrderBy(row => row.Status == "ready" ? 0 : 1).ThenByDescending(row => row.UpdatedAt)
            .FirstOrDefault();
        return (eventGuid, current);
    }

    private Task<string> EventInputVersionAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.EventRows.AsNoTracking().Where(row => row.Id == eventId).Select(row => row.InputVersion)
            .SingleAsync(cancellationToken);

    private Task<BackgroundJobEntity?> FindActiveJobAsync(UserContext user, string companyId, Guid eventId, Role role,
        string inputVersion, CancellationToken cancellationToken) =>
        jobs.FindActiveAsync(new GenerateInsightJob(companyId, eventId.ToString(), role, inputVersion), user.UserId,
            companyId, cancellationToken);

    private static AccessException AnalysisFailed() =>
        new(409, "analysis_failed", "Подготовка карточки не удалась. Повтор запускает оператор.");
}