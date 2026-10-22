using System.Text.Json;
using AlfaNews.Core.Data;
using AlfaNews.Core.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlfaNews.Core.Processing;

public sealed class JobDispatcher(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<JobDispatcher> logger)
{
    public async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
        var job = await db.BackgroundJobRows.Where(row => row.Status == "queued")
            .OrderByDescending(row => row.Priority).ThenBy(row => row.CreatedAt).ThenBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
            return false;
        job.Status = "running";
        job.Stage = "running";
        job.StartedAt = timeProvider.GetUtcNow();
        job.FinishedAt = null;
        job.Attempts++;
        job.LastError = null;
        job.ResultType = null;
        job.ResultId = null;
        await db.SaveChangesAsync(cancellationToken);
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
}