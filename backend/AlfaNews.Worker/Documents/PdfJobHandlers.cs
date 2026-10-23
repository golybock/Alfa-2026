using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.Documents;

public sealed class IndexPdfJobHandler(PdfProcessing service) : IJobHandler
{
    public string Kind => JobKinds.Name<IndexPdfJob>();
    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        service.PrepareAsync(job, job.Payload.Deserialize<IndexPdfJob>(JobKinds.JsonOptions)!, cancellationToken);
}

public sealed class OcrPdfPageJobHandler(PdfProcessing service) : IJobHandler
{
    public string Kind => JobKinds.Name<OcrPdfPageJob>();
    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        service.RecognizeAsync(job, job.Payload.Deserialize<OcrPdfPageJob>(JobKinds.JsonOptions)!, cancellationToken);
}

public sealed class FinalizePdfJobHandler(PdfProcessing service) : IJobHandler
{
    public string Kind => JobKinds.Name<FinalizePdfJob>();
    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        service.FinalizeAsync(job.Payload.Deserialize<FinalizePdfJob>(JobKinds.JsonOptions)!, cancellationToken);
}