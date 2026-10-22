using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker;

public sealed class CollectNewsJobHandler(CollectNewsService service) : IJobHandler
{
    public string Kind => JobKinds.Name<CollectNewsJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        var payload = job.Payload.Deserialize<CollectNewsJob>(JobKinds.JsonOptions)!;
        await service.CollectAsync(payload.SourceIds, cancellationToken);
        return new JobResult("collected");
    }
}