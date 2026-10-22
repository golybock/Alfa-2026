using System.Text.Json;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker;

public sealed class ConsoleMessageJobHandler(ILogger<ConsoleMessageJobHandler> logger) : IJobHandler
{
    public string Kind => JobKinds.Name<ConsoleMessageJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = job.Payload.Deserialize<ConsoleMessageJob>(JobKinds.JsonOptions)!;
        logger.LogInformation("Тестовое задание {JobId}: {Text}", job.Id, payload.Text);
        return Task.FromResult(new JobResult());
    }
}