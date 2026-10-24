using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.News;

public sealed class CollectManualNewsJobHandler(CollectNewsService service, NewsProcessingSchedule schedule) : IJobHandler
{
    public string Kind => JobKinds.Name<CollectManualNewsJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        var payload = job.Payload.Deserialize<CollectManualNewsJob>(JobKinds.JsonOptions)!;
        if (payload.Request.SourceId is null || payload.Request.Url is null)
            return new JobResult(ErrorCode: "invalid_payload");
        var result = await service.CollectManualAsync(payload.Request.SourceId, payload.Request.Url, cancellationToken);
        if (result.ErrorCode is null)
            await schedule.EnqueueAsync(cancellationToken);
        return new JobResult(Stage: result.Stage, ErrorCode: result.ErrorCode);
    }
}