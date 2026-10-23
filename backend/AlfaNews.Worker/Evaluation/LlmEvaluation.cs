using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Worker.Evaluation;

public sealed class LlmEvaluation(AlfaNewsDbContext db, LlmGateway gateway, LlmOptions options,
    TimeProvider timeProvider, ILogger<LlmEvaluation> logger)
{
    public async Task RunAsync(string inputPath, string reportPath, CancellationToken cancellationToken,
        IReadOnlySet<string>? selectedCases = null)
    {
        var input = await File.ReadAllTextAsync(inputPath, cancellationToken);
        var suite = JsonSerializer.Deserialize<EvaluationSuite>(input, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Пустой набор проверок.");
        if (selectedCases?.Any(name => !suite.Cases.Any(test => test.Name == name)) == true)
            throw new ArgumentException("Неизвестное имя проверки.");
        var job = new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            Kind = "LlmEvaluation",
            Status = "running",
            Stage = "evaluation",
            Payload = JsonSerializer.SerializeToElement(new { suite.Name }),
            CreatedAt = timeProvider.GetUtcNow(),
            StartedAt = timeProvider.GetUtcNow()
        };
        db.BackgroundJobRows.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        var results = new List<object>();
        var failedCases = 0;
        foreach (var test in suite.Cases.Where(test => selectedCases is null || selectedCases.Contains(test.Name)))
        {
            if (!Enum.TryParse<LlmTask>(test.Task, out var task) || task == LlmTask.Repair)
                throw new ArgumentException("Неизвестная задача проверки.");
            var image = test.ImagePath is null ? null : new LlmImage(await File.ReadAllBytesAsync(
                Path.GetFullPath(test.ImagePath, Path.GetDirectoryName(Path.GetFullPath(inputPath))!), cancellationToken));
            var candidates = new List<string>();
            var request = new LlmRequest(job.Id, suite.Name + ":" + test.Name, LlmPromptCatalog.Hash(input), task,
                test.Values, Image: image, Runtime: new LlmRuntime(), CaptureResponse: candidates.Add);
            var replay = false;
            string? error = null;
            string? label = null;
            JsonElement? candidate = null;
            IReadOnlyList<string> diagnostics = [];
            try
            {
                switch (task)
                {
                    case LlmTask.NewsTriage:
                        var triage = await ExecuteAsync<NewsTriage>(request, test, cancellationToken);
                        replay = triage.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(triage.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.NewsExtract:
                        var news = await ExecuteAsync<NewsExtractionDraft>(request, test, cancellationToken);
                        replay = news.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(news.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.PdfOcr:
                        var pdf = await ExecuteAsync<PdfPageExtraction>(request, test, cancellationToken);
                        replay = pdf.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(pdf.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.Plan:
                        var plan = await ExecuteAsync<AgentPlan>(request, test, cancellationToken);
                        replay = plan.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(plan.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.Draft:
                        var card = await ExecuteAsync<InsightDraft>(request, test, cancellationToken);
                        replay = card.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(card.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.Critic:
                        var critic = await ExecuteAsync<CriticResult>(request, test, cancellationToken);
                        replay = critic.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(critic.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.Chat:
                        var chat = await ExecuteAsync<ChatDraft>(request, test, cancellationToken);
                        replay = chat.CacheHit;
                        candidate = JsonSerializer.SerializeToElement(chat.Value, LlmOutputValidator.JsonOptions);
                        break;
                    case LlmTask.ScopeGate:
                        var result = await ExecuteAsync<ScopeGateResult>(request, test, cancellationToken);
                        label = result.Value.Label.ToString();
                        replay = result.CacheHit;
                        break;
                }
                if (test.ExpectedLabel is not null && !string.Equals(label, test.ExpectedLabel, StringComparison.OrdinalIgnoreCase))
                    error = "unexpected_label";
            }
            catch (LlmException exception)
            {
                error = exception.Code;
                diagnostics = exception.Diagnostics;
            }
            results.Add(new
            {
                test.Name,
                test.Task,
                Error = error,
                Diagnostics = diagnostics,
                CacheHit = replay,
                Label = label,
                Candidate = candidate,
                Attempts = candidates
            });
            if (error is not null)
                failedCases++;
            logger.LogInformation("Проверка LLM {Name}: {Result}.", test.Name, error ?? "passed");
        }
        var usage = await db.LlmUsageRows.AsNoTracking().Where(row => db.AgentRunRows
                .Any(run => run.Id == row.RunId && run.JobId == job.Id))
            .OrderBy(row => row.CreatedAt).Select(row => new
            {
                row.Stage,
                row.Model,
                row.Attempt,
                row.InputTokens,
                row.OutputTokens,
                row.ReasoningTokens,
                row.ActualCostRub,
                row.ReservedCostRub,
                row.DurationMs,
                row.CacheHit,
                row.Error
            }).ToListAsync(cancellationToken);
        job.FinishedAt = timeProvider.GetUtcNow();
        job.Status = failedCases == 0 ? "done" : "failed";
        job.LastError = failedCases == 0 ? null : "evaluation_failed";
        await db.SaveChangesAsync(cancellationToken);
        var report = new
        {
            suite.Name,
            options.UseMock,
            options.StructuredOutput,
            options.DeepSeekThinkingEnabled,
            options.DeepSeekReasoningLevel,
            options.GlmReasoningLevel,
            GeneralMaxOutputTokens = options.MaxOutputTokens,
            options.ChatMaxOutputTokens,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            Status = "schema_and_code_checks_only_manual_semantic_review_required",
            FailedCases = failedCases,
            KnownCostRub = usage.Sum(row => row.ActualCostRub ?? 0),
            UnknownCostRequests = usage.Count(row => row.ActualCostRub is null),
            Cases = results,
            Usage = usage
        };
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }), cancellationToken);
    }

    public sealed record EvaluationSuite(string Name, IReadOnlyList<EvaluationCase> Cases);
    public sealed record EvaluationCase(string Name, string Task, IReadOnlyDictionary<string, JsonElement> Values,
        string? ImagePath = null, string? ExpectedLabel = null, string? PreviousResponse = null,
        IReadOnlyList<string>? Diagnostics = null);

    private Task<LlmResult<T>> ExecuteAsync<T>(LlmRequest request, EvaluationCase test, CancellationToken cancellationToken) =>
        test.PreviousResponse is null ? gateway.GenerateAsync<T>(request, cancellationToken)
            : gateway.RepairAsync<T>(request, test.PreviousResponse, test.Diagnostics ?? [], cancellationToken);
}