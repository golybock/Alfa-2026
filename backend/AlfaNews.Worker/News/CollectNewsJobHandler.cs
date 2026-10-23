using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.News;

public sealed class CollectNewsJobHandler(CollectNewsService service, NewsProcessingSchedule schedule) : IJobHandler
{
    public string Kind => JobKinds.Name<CollectNewsJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        var payload = job.Payload.Deserialize<CollectNewsJob>(JobKinds.JsonOptions)!;
        await service.CollectAsync(payload.SourceIds, cancellationToken);
        await schedule.EnqueueAsync(cancellationToken);
        return new JobResult("collected");
    }
}