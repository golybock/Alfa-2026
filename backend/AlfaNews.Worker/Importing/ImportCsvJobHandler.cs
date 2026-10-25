using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Importing;

public sealed class ImportCsvJobHandler(CsvImportService service, AlfaNewsDbContext db) : IJobHandler
{
    public string Kind => JobKinds.Name<ImportCsvJob>();

    public async Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken)
    {
        await service.ImportAsync(job.Payload.Deserialize<ImportCsvJob>(JobKinds.JsonOptions)!, cancellationToken,
            job.Id);
        var completed = await db.BackgroundJobRows.AsNoTracking()
            .SingleAsync(row => row.Id == job.Id && row.Status == "done", cancellationToken);
        return new JobResult(completed.Stage, completed.ResultType, completed.ResultId);
    }
}