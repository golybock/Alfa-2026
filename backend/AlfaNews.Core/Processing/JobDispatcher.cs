using System.Text.Json;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlfaNews.Core.Processing;

public sealed class JobDispatcher(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<JobDispatcher> logger)
{
    public Task<bool> DispatchNextAsync(CancellationToken cancellationToken) => DispatchNextAsync(null, cancellationToken);

    public async Task<bool> DispatchNextAsync(IReadOnlyCollection<string>? kinds, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
        var job = await ClaimAsync(db, kinds, cancellationToken);
        if (job is null)
            return false;
        try
        {
            var payloadType = JobKinds.PayloadType(job.Kind);
            if (payloadType is null)
            {
                job.Status = "failed";
                job.LastError = "not_implemented";
            }
            else
            {
                _ = job.Payload.Deserialize(payloadType, JobKinds.JsonOptions)
                    ?? throw new JsonException("Пустые данные задания.");
                await using var executionScope = scopes.CreateAsyncScope();
                var handler = executionScope.ServiceProvider.GetServices<IJobHandler>().SingleOrDefault(candidate => candidate.Kind == job.Kind);
                if (handler is null)
                {
                    job.Status = "failed";
                    job.LastError = "not_implemented";
                }
                else
                {
                    var result = await handler.HandleAsync(job, cancellationToken);
                    job.Status = result.ErrorCode is null ? "done" : "failed";
                    job.Stage = result.Stage;
                    job.LastError = result.ErrorCode;
                    if (result.ErrorCode is null)
                    {
                        job.ResultType = result.ResultType;
                        job.ResultId = result.ResultId;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            job.Status = "failed";
            job.LastError = exception switch
            {
                JsonException => "invalid_payload",
                LlmException llm => llm.Code,
                _ => "job_failed"
            };
            logger.LogError("Ошибка задания {JobId} вида {Kind}: {ErrorType}.", job.Id, job.Kind, exception.GetType().Name);
        }
        if (job.Status == "failed")
            job.Stage = "failed";
        job.FinishedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<BackgroundJobEntity?> ClaimAsync(AlfaNewsDbContext db, IReadOnlyCollection<string>? kinds,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var queued = db.BackgroundJobRows.AsNoTracking().Where(row => row.Status == "queued");
            if (kinds is not null)
                queued = queued.Where(row => kinds.Contains(row.Kind));
            var candidate = await queued.OrderByDescending(row => row.Priority).ThenBy(row => row.CreatedAt).ThenBy(row => row.Id)
                .Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken);
            if (candidate is not { } id)
                return null;
            var claimed = await db.BackgroundJobRows.Where(row => row.Id == id && row.Status == "queued")
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "running")
                    .SetProperty(row => row.Stage, "running").SetProperty(row => row.StartedAt, (DateTimeOffset?)timeProvider.GetUtcNow())
                    .SetProperty(row => row.FinishedAt, (DateTimeOffset?)null).SetProperty(row => row.Attempts, row => row.Attempts + 1)
                    .SetProperty(row => row.LastError, (string?)null).SetProperty(row => row.ResultType, (string?)null)
                    .SetProperty(row => row.ResultId, (string?)null), cancellationToken);
            if (claimed == 1)
                return await db.BackgroundJobRows.SingleAsync(row => row.Id == id, cancellationToken);
        }
    }
}