using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Profiles;

public sealed class RebuildProfilesJobHandler(ProfileBuildService service, AlfaNewsDbContext db) : IJobHandler
{
    public string Kind => JobKinds.Name<RebuildProfilesJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        await service.BuildAsync(job.Payload.Deserialize<RebuildProfilesJob>(JobKinds.JsonOptions)!, cancellationToken,
            job.Id);
        var completed = await db.BackgroundJobRows.AsNoTracking()
            .SingleAsync(row => row.Id == job.Id && row.Status == "done", cancellationToken);
        return new JobResult(completed.Stage, completed.ResultType, completed.ResultId);
    }
}