using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.Insights;

public sealed class GenerateInsightJobHandler(InsightAgent agent) : IJobHandler
{
    public string Kind => JobKinds.Name<GenerateInsightJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        agent.GenerateAsync(job, job.Payload.Deserialize<GenerateInsightJob>(JobKinds.JsonOptions)!, cancellationToken);
}