using System.Security.Cryptography;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Documents;

public sealed class PdfRegistration(AlfaNewsDbContext db, JobQueue queue, PdfOptions options, PdfTools tools, TimeProvider time)
{
    public async Task<DocumentAcceptedResponse> RegisterAsync(Stream input, string title, DateOnly? publishedOn,
        string? sourceUrl, Guid? uploadedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 500 || sourceUrl is { Length: > 2000 }
            || sourceUrl is not null && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0))
            throw new AccessException(400, "invalid_input", "Некорректные метаданные PDF.");
        var relative = $"files/{Guid.NewGuid():N}.pdf";
        var path = options.Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var retained = false;
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                var buffer = new byte[65536];
                long size = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > PdfOptions.MaxFileBytes)
                        throw new AccessException(413, "invalid_input", "PDF превышает 50 MiB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            string hash;
            await using (var file = File.OpenRead(path))
            {
                var header = new byte[5];
                if (await file.ReadAsync(header, cancellationToken) != 5 || !header.AsSpan().SequenceEqual("%PDF-"u8))
                    throw new AccessException(415, "invalid_input", "Требуется файл PDF.");
                file.Position = 0;
                hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
            }
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({hash}, 0))", cancellationToken);
            var existing = await db.ResearchDocRows.SingleOrDefaultAsync(row => row.Sha256 == hash, cancellationToken);
            if (existing is not null)
            {
                var storedPath = options.Resolve(existing.FilePath);
                if (!File.Exists(storedPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(storedPath)!);
                    File.Move(path, storedPath);
                }
                var response = await ExistingAsync(existing, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return response;
            }
            var count = await tools.PageCountAsync(path, cancellationToken);
            var document = new ResearchDocEntity
            {
                Id = Guid.NewGuid(),
                Sha256 = hash,
                FilePath = relative,
                Title = title.Trim(),
                PublishedOn = publishedOn,
                SourceUrl = sourceUrl,
                PageCount = count,
                OcrStatus = "queued",
                ReviewStatus = "not_reviewed",
                Version = hash,
                UploadedBy = uploadedBy,
                CreatedAt = time.GetUtcNow()
            };
            db.ResearchDocRows.Add(document);
            var job = await queue.EnqueueAsync(new IndexPdfJob(document.Id.ToString()), uploadedBy, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            retained = true;
            return new DocumentAcceptedResponse(document.Id.ToString(), job.Id.ToString(), ReviewStatus.NotReviewed);
        }
        finally
        {
            if (!retained)
                File.Delete(path);
        }
    }

    private async Task<DocumentAcceptedResponse> ExistingAsync(ResearchDocEntity document, CancellationToken cancellationToken)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new IndexPdfJob(document.Id.ToString()), JobKinds.JsonOptions);
        var job = await db.BackgroundJobRows.Where(row => row.Kind == "IndexPdf" && row.Payload.Equals(payload))
            .OrderByDescending(row => row.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            var pages = await db.ResearchPageRows.AsNoTracking().Where(row => row.DocumentId == document.Id)
                .Select(row => new { row.OcrStatus, row.LastError }).ToArrayAsync(cancellationToken);
            var keyRequired = DocumentStatuses.WaitsForKey(document.OcrStatus, pages.Select(row => (row.OcrStatus, row.LastError)).ToArray());
            job = new BackgroundJobEntity
            {
                Id = Guid.NewGuid(),
                Kind = "IndexPdf",
                Payload = payload,
                Status = document.OcrStatus is "ready" or "partial" ? "done" : "failed",
                Stage = document.OcrStatus,
                Priority = 30,
                CreatedAt = time.GetUtcNow(),
                FinishedAt = time.GetUtcNow(),
                ResultType = "document",
                ResultId = document.Id.ToString(),
                LastError = keyRequired ? DocumentStatuses.KeyRequired : document.OcrStatus == "failed" ? "processing_failed" : null
            };
            db.BackgroundJobRows.Add(job);
            await db.SaveChangesAsync(cancellationToken);
        }
        return new DocumentAcceptedResponse(document.Id.ToString(), job.Id.ToString(),
            Enum.Parse<ReviewStatus>(document.ReviewStatus.Replace("_", ""), true));
    }
}