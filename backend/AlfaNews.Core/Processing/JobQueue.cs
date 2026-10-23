using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Processing;

public sealed class JobQueue(AlfaNewsDbContext db, TimeProvider timeProvider)
{
    public async Task<BackgroundJobEntity?> FindActiveAsync<T>(T payload, Guid? ownerUserId, string? companyId,
        CancellationToken cancellationToken) where T : class
    {
        var kind = JobKinds.Name<T>();
        var json = JsonSerializer.SerializeToElement(payload, JobKinds.JsonOptions);
        return await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == kind && (row.Status == "queued" || row.Status == "running")
                && row.OwnerUserId == ownerUserId && row.CompanyId == companyId && row.Payload.Equals(json))
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BackgroundJobEntity> EnqueueAsync<T>(T payload, Guid? ownerUserId, string? companyId,
        CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload is GenerateInsightJob insight && insight.CompanyId != companyId
            || payload is RefreshFeedJob feed && feed.CompanyId != companyId
            || payload is AnswerChatJob chat && chat.UserId != ownerUserId?.ToString())
            throw new ArgumentException("Контекст задания не совпадает с владельцем или компанией.", nameof(payload));
        var active = await FindActiveAsync(payload, ownerUserId, companyId, cancellationToken);
        if (active is not null)
            return active;
        var job = new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            Kind = JobKinds.Name<T>(),
            Payload = JsonSerializer.SerializeToElement(payload, JobKinds.JsonOptions),
            Status = "queued",
            Stage = "queued",
            Priority = JobKinds.Priority<T>(),
            OwnerUserId = ownerUserId,
            CompanyId = companyId,
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.BackgroundJobRows.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<int> RecoverAsync(CancellationToken cancellationToken) => db.BackgroundJobRows
        .Where(row => row.Status == "running")
        .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "queued")
            .SetProperty(row => row.Stage, "queued").SetProperty(row => row.StartedAt, (DateTimeOffset?)null)
            .SetProperty(row => row.FinishedAt, (DateTimeOffset?)null), cancellationToken);

    public async Task<bool> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var changed = await db.BackgroundJobRows.Where(row => row.Id == id && (row.Status == "failed" || row.Status == "running"))
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "queued")
                .SetProperty(row => row.Stage, "queued").SetProperty(row => row.StartedAt, (DateTimeOffset?)null)
                .SetProperty(row => row.FinishedAt, (DateTimeOffset?)null).SetProperty(row => row.LastError, (string?)null)
                .SetProperty(row => row.ResultType, (string?)null).SetProperty(row => row.ResultId, (string?)null), cancellationToken);
        return changed == 1;
    }

    public static JobResponse ToResponse(BackgroundJobEntity job, string requestId)
    {
        var error = job.LastError switch
        {
            null => null,
            "not_implemented" => new ApiError("not_implemented", "Обработчик задания пока не подключён.", requestId),
            "invalid_payload" => new ApiError("invalid_payload", "Некорректные данные задания.", requestId),
            "llm_key_required" => new ApiError("llm_key_required", "Требуется ключ модели.", requestId),
            "budget_exhausted" or "budget_unknown_cost" => new ApiError("budget_exhausted", "Недостаточно доступного бюджета модели.", requestId),
            "llm_timeout" => new ApiError("processing_failed", "Модель не ответила вовремя.", requestId),
            _ => new ApiError("job_failed", "Не удалось выполнить задание.", requestId)
        };
        var safeResult = job.Status == "done" && job.ResultType is "insight" or "chat_turn" or "document" or "feed";
        return new JobResponse(job.Id.ToString(), Enum.Parse<JobStatus>(job.Status, true), job.Stage,
            safeResult ? job.ResultType : null, safeResult ? job.ResultId : null, error);
    }
}