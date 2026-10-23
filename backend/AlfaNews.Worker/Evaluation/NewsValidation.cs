using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Evaluation;

public sealed class NewsValidation(AlfaNewsDbContext db, JobQueue queue, JobDispatcher dispatcher,
    LlmGateway gateway, LlmOptions options, NewsProcessing processing)
{
    public async Task RunAsync(string reportPath, string sourceId, int parseLimit, CancellationToken cancellationToken)
    {
        if (!db.Database.GetDbConnection().Database.StartsWith("alfanews_a07_", StringComparison.Ordinal)
            || await db.NewsRawRows.AnyAsync(cancellationToken) || await db.BackgroundJobRows.AnyAsync(cancellationToken)
            || parseLimit is < 1 or > 10)
            throw new InvalidOperationException("Проверка требует пустую отдельную БД alfanews_a07_* и лимит разбора 1–10.");
        options.DailyLimitRub = Math.Min(options.DailyLimitRub, 150m);
        await queue.EnqueueAsync(new CollectNewsJob([sourceId]), null, null, cancellationToken);
        await dispatcher.DispatchNextAsync(cancellationToken);
        db.ChangeTracker.Clear();
        var batch = await db.BackgroundJobRows.AsNoTracking().Where(row => row.Kind == "TriageNews" && row.Status == "queued")
            .OrderBy(row => row.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Источник не дал новых новостей для фильтра.");
        var ids = batch.Payload.Deserialize<TriageNewsJob>(JobKinds.JsonOptions)!.NewsRawIds.Select(Guid.Parse).ToArray();
        var headlines = await db.NewsRawRows.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id)
            .Select(row => new { id = row.Id.ToString(), title = row.Title }).ToArrayAsync(cancellationToken);
        await dispatcher.DispatchNextAsync(cancellationToken);
        db.ChangeTracker.Clear();
        var comparisonJob = new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            Kind = "LlmEvaluation",
            Status = "running",
            Stage = "comparison",
            Payload = JsonSerializer.SerializeToElement(new { sourceId }),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.BackgroundJobRows.Add(comparisonJob);
        await db.SaveChangesAsync(cancellationToken);
        var input = JsonSerializer.SerializeToElement(headlines);
        NewsTriage? glm = null;
        string? comparisonError = null;
        try
        {
            options.TriageUseGlm = true;
            var result = await gateway.GenerateAsync<NewsTriage>(new LlmRequest(comparisonJob.Id, "triage-glm:" + batch.Id,
                LlmPromptCatalog.Hash(input.GetRawText()), LlmTask.NewsTriage,
                new Dictionary<string, JsonElement> { ["INPUT_JSON"] = input }), cancellationToken);
            glm = result.Value;
        }
        catch (LlmException exception)
        {
            comparisonError = exception.Code;
        }
        finally
        {
            options.TriageUseGlm = false;
        }
        comparisonJob.Status = comparisonError is null ? "done" : "failed";
        comparisonJob.FinishedAt = DateTimeOffset.UtcNow;
        comparisonJob.LastError = comparisonError;
        await db.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < parseLimit; index++)
            if (!await dispatcher.DispatchNextAsync(cancellationToken))
                break;
        db.ChangeTracker.Clear();
        var accepted = await db.EventRows.AsNoTracking().OrderBy(row => row.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var paidBeforeReplay = await db.LlmUsageRows.CountAsync(row => row.Attempt > 0 && !row.CacheHit, cancellationToken);
        if (accepted is not null)
            await processing.ProcessAsync(comparisonJob, accepted.NewsRawId, cancellationToken);
        var paidAfterReplay = await db.LlmUsageRows.CountAsync(row => row.Attempt > 0 && !row.CacheHit, cancellationToken);
        var usage = await db.LlmUsageRows.AsNoTracking().OrderBy(row => row.CreatedAt)
            .Select(row => new
            {
                row.Stage,
                row.Model,
                row.ActualCostRub,
                row.ReservedCostRub,
                row.DurationMs,
                row.InputTokens,
                row.OutputTokens,
                row.ReasoningTokens,
                row.CacheHit,
                row.Error
            }).ToArrayAsync(cancellationToken);
        var results = await db.NewsRawRows.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Title, row.Importance, row.Status, row.RejectionReason, row.LastError, row.FilterJson })
            .ToArrayAsync(cancellationToken);
        var report = new
        {
            SourceId = sourceId,
            ParseLimit = parseLimit,
            options.UseMock,
            BudgetRub = options.DailyLimitRub,
            Collected = await db.NewsRawRows.CountAsync(cancellationToken),
            Accepted = await db.EventRows.CountAsync(cancellationToken),
            PaidBeforeReplay = paidBeforeReplay,
            PaidAfterReplay = paidAfterReplay,
            CostRub = usage.Sum(row => row.ActualCostRub ?? 0),
            UnknownCostRequests = usage.Count(row => row.ActualCostRub is null),
            ComparisonError = comparisonError,
            Glm = glm,
            Results = results,
            Usage = usage
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(LlmOutputValidator.JsonOptions)
        { WriteIndented = true }), cancellationToken);
    }
}