using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;

namespace AlfaNews.Core.Documents;

public sealed class PdfProcessing(AlfaNewsDbContext db, JobQueue queue, PdfOptions options, PdfTools tools, LlmGateway gateway,
    ILogger<PdfProcessing> logger)
{
    public async Task<JobResult> PrepareAsync(BackgroundJobEntity job, IndexPdfJob payload, CancellationToken cancellationToken)
    {
        var document = await DocumentAsync(payload.DocumentId, cancellationToken);
        if (document.OcrStatus is "ready" or "partial")
            return new JobResult(ResultType: "document", ResultId: document.Id.ToString());
        document.OcrStatus = "processing";
        for (var number = 1; number <= document.PageCount; number++)
        {
            var page = await db.ResearchPageRows.SingleOrDefaultAsync(row => row.DocumentId == document.Id && row.PageNo == number, cancellationToken);
            if (page is null)
            {
                page = new ResearchPageEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    PageNo = number,
                    ImagePath = $"pages/{document.Sha256}/{number}.png",
                    OcrStatus = "queued",
                    ReviewStatus = "not_reviewed"
                };
                db.ResearchPageRows.Add(page);
            }
            if (page.OcrStatus == "ready")
                continue;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await queue.EnqueueAsync(new OcrPdfPageJob(document.Id.ToString(), number), job.OwnerUserId, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await EnqueueFinalizationAsync(document, job.OwnerUserId, cancellationToken);
        return new JobResult(ResultType: "document", ResultId: document.Id.ToString());
    }

    public async Task<JobResult> RecognizeAsync(BackgroundJobEntity job, OcrPdfPageJob payload, CancellationToken cancellationToken)
    {
        var document = await DocumentAsync(payload.DocumentId, cancellationToken);
        var page = await db.ResearchPageRows.SingleAsync(row => row.DocumentId == document.Id && row.PageNo == payload.PageNo, cancellationToken);
        if (page.OcrStatus == "ready")
        {
            await EnqueueFinalizationAsync(document, job.OwnerUserId, cancellationToken);
            return new JobResult();
        }
        page.OcrStatus = "processing";
        page.LastError = null;
        document.OcrStatus = "processing";
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var imagePath = options.Resolve(page.ImagePath);
            if (!File.Exists(imagePath))
                await tools.RenderAsync(options.Resolve(document.FilePath), page.PageNo, imagePath, cancellationToken);
            var factors = await db.FactorRows.AsNoTracking().OrderBy(row => row.Code)
                .Select(row => new { code = row.Code, name = row.Name }).ToArrayAsync(cancellationToken);
            var taxonomy = JsonSerializer.SerializeToElement(new
            {
                factors,
                okved2 = Array.Empty<string>(),
                countries = Array.Empty<string>(),
                regions = Array.Empty<string>(),
                commodities = Array.Empty<string>()
            });
            var metadata = JsonSerializer.SerializeToElement(new
            {
                document_id = document.Id,
                page_no = page.PageNo,
                title = document.Title,
                published_on = document.PublishedOn
            });
            var inputVersion = LlmPromptCatalog.Hash(document.Sha256 + ":" + page.PageNo + ":png2200-v1:" + metadata.GetRawText() + taxonomy.GetRawText());
            var request = new LlmRequest(job.Id, $"pdf:{document.Sha256}:page:{page.PageNo}", inputVersion, LlmTask.PdfOcr,
                new Dictionary<string, JsonElement>
                {
                    ["PAGE_METADATA_JSON"] = metadata,
                    ["TAXONOMY_JSON"] = taxonomy
                }, Image: new LlmImage(await File.ReadAllBytesAsync(imagePath, cancellationToken)),
                ValidateOutput: PdfExtractionChecks.Validate, ValidationVersion: PdfExtractionChecks.Version);
            var result = await gateway.GenerateAsync<PdfPageExtraction>(request, cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var block in result.Value.Blocks)
            {
                var chunk = await db.ResearchChunkRows.SingleOrDefaultAsync(row => row.PageId == page.Id && row.LocalId == block.LocalId, cancellationToken);
                if (chunk is not null)
                    continue;
                db.ResearchChunkRows.Add(new ResearchChunkEntity
                {
                    Id = Guid.NewGuid(),
                    PageId = page.Id,
                    LocalId = block.LocalId,
                    Kind = JsonSerializer.SerializeToElement(block.Kind, JobKinds.JsonOptions).GetString()!,
                    Text = block.Text,
                    TablesJson = JsonSerializer.SerializeToElement(block.Tables, JobKinds.JsonOptions),
                    ChartData = JsonSerializer.SerializeToElement(block.Charts.Select(chart => new ReviewableChartDto(chart, ReviewStatus.NotReviewed)), JobKinds.JsonOptions),
                    TagsJson = JsonSerializer.SerializeToElement(block.Tags, JobKinds.JsonOptions),
                    ReviewStatus = "not_reviewed",
                    Version = inputVersion,
                    SearchText = NpgsqlTsVector.Empty
                });
            }
            page.OcrJson = JsonSerializer.SerializeToElement(result.Value, JobKinds.JsonOptions);
            page.OcrStatus = "ready";
            await db.SaveChangesAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE research_chunk SET search_text = to_tsvector('russian', text) WHERE page_id = {page.Id}", cancellationToken);
            await EnqueueFinalizationAsync(document, job.OwnerUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new JobResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            page = await db.ResearchPageRows.SingleAsync(row => row.Id == page.Id, cancellationToken);
            document = await DocumentAsync(payload.DocumentId, cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            page.OcrStatus = "failed";
            page.LastError = exception is LlmException llm ? llm.Code : "pdf_page_failed";
            logger.LogWarning("OCR документа {DocumentId}, страница {PageNo}, задание {JobId}: {ErrorCode}.",
                document.Id, page.PageNo, job.Id, page.LastError);
            await db.SaveChangesAsync(cancellationToken);
            await EnqueueFinalizationAsync(document, job.OwnerUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new JobResult(ErrorCode: page.LastError);
        }
    }

    public async Task<JobResult> FinalizeAsync(FinalizePdfJob payload, CancellationToken cancellationToken)
    {
        var document = await DocumentAsync(payload.DocumentId, cancellationToken);
        var statuses = await db.ResearchPageRows.Where(row => row.DocumentId == document.Id).Select(row => row.OcrStatus).ToArrayAsync(cancellationToken);
        if (statuses.Length != document.PageCount || statuses.Any(status => status is "queued" or "processing"))
            return new JobResult(Stage: "waiting_pages");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        document.OcrStatus = statuses.All(status => status == "ready") ? "ready" : statuses.Any(status => status == "ready") ? "partial" : "failed";
        await db.SaveChangesAsync(cancellationToken);
        var chunks = await (from chunk in db.ResearchChunkRows.AsNoTracking()
                            join page in db.ResearchPageRows on chunk.PageId equals page.Id
                            where page.DocumentId == document.Id && page.OcrStatus == "ready"
                            select new { chunk.Id, chunk.Version }).ToArrayAsync(cancellationToken);
        foreach (var chunk in chunks)
        {
            var payloadJson = JsonSerializer.SerializeToElement(new EmbedContentJob("research_chunk", chunk.Id.ToString(), chunk.Version), JobKinds.JsonOptions);
            if (!await db.BackgroundJobRows.AnyAsync(row => row.Kind == "EmbedContent" && row.Payload.Equals(payloadJson), cancellationToken))
                await queue.EnqueueAsync(new EmbedContentJob("research_chunk", chunk.Id.ToString(), chunk.Version), document.UploadedBy, null, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new JobResult(Stage: document.OcrStatus, ResultType: "document", ResultId: document.Id.ToString());
    }

    private Task<ResearchDocEntity> DocumentAsync(string id, CancellationToken cancellationToken) =>
        db.ResearchDocRows.SingleAsync(row => row.Id == Guid.Parse(id), cancellationToken);

    private async Task EnqueueFinalizationAsync(ResearchDocEntity document, Guid? owner, CancellationToken cancellationToken)
    {
        var statuses = await db.ResearchPageRows.Where(row => row.DocumentId == document.Id).Select(row => row.OcrStatus).ToArrayAsync(cancellationToken);
        if (statuses.Length == document.PageCount && statuses.All(status => status is "ready" or "failed"))
            await queue.EnqueueAsync(new FinalizePdfJob(document.Id.ToString()), owner, null, cancellationToken);
    }
}