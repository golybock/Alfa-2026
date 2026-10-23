using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.News;

public sealed class TriageNewsJobHandler(NewsTriageService service) : IJobHandler
{
    public string Kind => JobKinds.Name<TriageNewsJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        await service.ProcessAsync(job, job.Payload.Deserialize<TriageNewsJob>(JobKinds.JsonOptions)!, cancellationToken);
        return new JobResult("triaged");
    }
}