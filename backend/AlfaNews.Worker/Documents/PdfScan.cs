using System.Security.Cryptography;
using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Documents;

public sealed class PdfScan(
    AlfaNewsDbContext db,
    PdfRegistration registration,
    PdfOptions options,
    ILogger<PdfScan> logger)
{
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var inputRoot = options.InputDirectory;
        var indexPath = Path.Combine(inputRoot, "bank_research", "index.json");
        var entries = File.Exists(indexPath)
            ? JsonSerializer.Deserialize<BankDocumentIndexEntry[]>(
                await File.ReadAllTextAsync(indexPath, cancellationToken), JobKinds.JsonOptions) ?? []
            : [];
        var metadata = entries.ToDictionary(entry => entry.Sha256, StringComparer.Ordinal);
        var inbox = options.Resolve("inbox");
        Directory.CreateDirectory(inbox);
        var files = (Directory.Exists(inputRoot) ? Directory.EnumerateFiles(inputRoot) : [])
            .Concat(Directory.EnumerateFiles(inbox)).Where(path =>
                string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).ToArray();
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (new FileInfo(path).LastWriteTimeUtc > DateTime.UtcNow.AddSeconds(-5))
                    continue;
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (file.Length > PdfOptions.MaxFileBytes)
                    continue;
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
                var document = await db.ResearchDocRows.AsNoTracking()
                    .SingleOrDefaultAsync(row => row.Sha256 == hash, cancellationToken);
                if (document is not null)
                {
                    var stored = options.Resolve(document.FilePath);
                    if (!File.Exists(stored))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
                        var temporary = stored + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            file.Position = 0;
                            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                                             FileShare.None))
                                await file.CopyToAsync(output, cancellationToken);
                            File.Move(temporary, stored);
                        }
                        finally
                        {
                            File.Delete(temporary);
                        }
                    }

                    continue;
                }

                metadata.TryGetValue(hash, out var entry);
                file.Position = 0;
                await registration.RegisterAsync(file, entry?.Title ?? Path.GetFileNameWithoutExtension(path),
                    entry?.PublishedOn,
                    entry?.SourceUrl, null, cancellationToken);
                logger.LogInformation("Зарегистрирован новый PDF с SHA-256 {Sha256}.", hash);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Не удалось зарегистрировать PDF: {ErrorType}.", exception.GetType().Name);
            }
        }

        var reviewed = entries.Where(entry => entry.ReviewStatus == ReviewStatus.Reviewed).Select(entry => entry.Sha256)
            .ToArray();
        var marked = await ReviewedResearch.ApplyAsync(db, reviewed, cancellationToken);
        if (marked > 0)
            logger.LogInformation("Обзоры банка из index.json отмечены проверенными: обновлено строк {Count}.", marked);
    }
}