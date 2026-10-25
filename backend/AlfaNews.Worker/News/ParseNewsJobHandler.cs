using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.News;

public sealed class ParseNewsJobHandler(NewsProcessing processing) : IJobHandler
{
    public string Kind => JobKinds.Name<ParseNewsJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        processing.ProcessAsync(job, Guid.Parse(job.Payload.Deserialize<ParseNewsJob>(JobKinds.JsonOptions)!.NewsRawId),
            cancellationToken);
}