using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.Feed;

public sealed class RefreshFeedJobHandler(FeedRefreshService service) : IJobHandler
{
    public string Kind => JobKinds.Name<RefreshFeedJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        service.RefreshAsync(job.Payload.Deserialize<RefreshFeedJob>(JobKinds.JsonOptions)!, cancellationToken);
}

public sealed class PrecomputeDemoJobHandler(FeedRefreshService service) : IJobHandler
{
    public string Kind => JobKinds.Name<PrecomputeDemoJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        service.PrecomputeDemoAsync(job.Payload.Deserialize<PrecomputeDemoJob>(JobKinds.JsonOptions)!, cancellationToken);
}