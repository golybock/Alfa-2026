using System.Diagnostics;
using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlfaNews.Core.Llm;

public sealed class LlmGateway(AlfaNewsDbContext db, LlmOptions options, LlmPromptCatalog prompts,
    ILlmClient client, LlmBudget budget, TimeProvider timeProvider, ILogger<LlmGateway> logger)
{
    public Task<LlmResult<T>> GenerateAsync<T>(LlmRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync<T>(request, null, null, cancellationToken);

    public Task<LlmResult<T>> RepairAsync<T>(LlmRequest original, string previous, IReadOnlyList<string> diagnostics,
        CancellationToken cancellationToken) => ExecuteAsync<T>(original, previous, diagnostics, cancellationToken);

    private async Task<LlmResult<T>> ExecuteAsync<T>(LlmRequest request, string? previous,
        IReadOnlyList<string>? diagnostics, CancellationToken cancellationToken)
    {
        if (request.Task == LlmTask.Repair || string.IsNullOrWhiteSpace(request.OperationKey)
            || string.IsNullOrWhiteSpace(request.InputVersion) || string.IsNullOrWhiteSpace(request.ValidationVersion))
            throw new ArgumentException("Требуется исходная задача и версии операции.");
        if ((request.Task == LlmTask.PdfOcr) != (request.Image is not null)
            || request.Image is { MediaType: not ("image/png" or "image/jpeg") })
            throw new ArgumentException("Для OCR требуется одно изображение страницы.");
        if (request.Task == LlmTask.Critic && (!options.CriticEnabled || previous is not null
            || options.CriticScope == "top_only" && request.Runtime?.Rank != 1))
            throw new LlmException("critic_disabled");
        var requestKey = LlmPromptCatalog.Hash(JsonSerializer.Serialize(new { request.OperationKey, request.InputVersion, options.UseMock }));
        var ownsRun = request.RunId is null;
        var run = request.RunId is { } runId
            ? await db.AgentRunRows.SingleAsync(row => row.Id == runId, cancellationToken)
            : new AgentRunEntity
            {
                Id = Guid.NewGuid(),
                JobId = request.JobId,
                InsightId = request.CardId,
                InputVersion = request.InputVersion,
                Status = "running",
                StartedAt = timeProvider.GetUtcNow()
            };
        if (run.JobId != request.JobId || run.InsightId != request.CardId || run.InputVersion != request.InputVersion)
            throw new ArgumentException("Контекст LLM не совпадает с прогоном.");
        if (ownsRun)
        {
            db.AgentRunRows.Add(run);
            await db.SaveChangesAsync(cancellationToken);
        }
        try
        {
            var result = await SendAsync<T>(request, run.Id, requestKey, previous, diagnostics, cancellationToken);
            if (ownsRun)
            {
                run.Status = "done";
                run.FinishedAt = timeProvider.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
        catch (Exception exception)
        {
            if (ownsRun)
            {
                run.Status = "failed";
                run.FinishedAt = timeProvider.GetUtcNow();
            }
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (exception is LlmException failure)
                db.LlmUsageRows.Add(new LlmUsageEntity
                {
                    Id = Guid.NewGuid(),
                    RunId = run.Id,
                    CardId = request.CardId,
                    Stage = LlmBudget.Stage(request.Task),
                    Model = options.Route(request.Task, request.Task).Model,
                    ConfigVersion = options.ConfigVersion,
                    RequestKey = requestKey,
                    ActualCostRub = 0,
                    Error = failure.Code,
                    CreatedAt = timeProvider.GetUtcNow()
                });
            await db.SaveChangesAsync(cleanup.Token);
            throw;
        }
    }

    private async Task<LlmResult<T>> SendAsync<T>(LlmRequest request, Guid runId, string requestKey, string? previous,
        IReadOnlyList<string>? diagnostics, CancellationToken cancellationToken)
    {
        var task = previous is null ? request.Task : LlmTask.Repair;
        var repairing = task == LlmTask.Repair;
        var transientRetries = 0;
        var originalStage = LlmBudget.Stage(request.Task);
        var originalPrompt = prompts.Render(request, request.Task, schemaInPrompt: !options.StructuredOutput);
        var originalCacheKey = CacheKey(request, request.Task, originalPrompt, options.Route(request.Task, request.Task));
        var since = await db.BackgroundJobRows.AsNoTracking().Where(row => row.Id == request.JobId)
            .Select(row => row.StartedAt).SingleOrDefaultAsync(cancellationToken) ?? DateTimeOffset.MinValue;
        var initialCount = await db.LlmUsageRows.CountAsync(row => row.RequestKey == requestKey && row.Attempt > 0
            && row.CreatedAt >= since && (row.Stage == originalStage || row.Stage == "repair"), cancellationToken);
        for (var attempt = 1; attempt <= Math.Max(1, 4 - initialCount); attempt++)
        {
            var route = options.Route(task, request.Task);
            var prompt = prompts.Render(request, task, previous, diagnostics, !options.StructuredOutput);
            var cacheKey = CacheKey(request, task, prompt, route);
            var configVersion = LlmPromptCatalog.Hash(JsonSerializer.Serialize(new
            {
                options.ConfigVersion,
                prompts.VersionHash,
                prompt.SchemaHash,
                request.ValidationVersion,
                SemanticVersion = LlmSemanticChecks.Version,
                SalvageVersion = LlmSalvage.Version,
                route.Model,
                route.Reasoning,
                route.ThinkingEnabled,
                route.MaxOutputTokens,
                options.StructuredOutput,
                options.UseMock
            }));
            var cached = await db.LlmCacheRows.AsNoTracking().SingleOrDefaultAsync(row => row.CacheKey == cacheKey, cancellationToken);
            if (cached is not null && LlmOutputValidator.Validate<T>(cached.ResponseJson.GetRawText(), prompt, request, out var cachedValue).Count == 0)
            {
                db.LlmUsageRows.Add(new LlmUsageEntity
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    CardId = request.CardId,
                    Stage = LlmBudget.Stage(task),
                    Model = route.Model,
                    ConfigVersion = configVersion,
                    RequestKey = requestKey,
                    CacheHit = true,
                    ActualCostRub = 0,
                    CreatedAt = timeProvider.GetUtcNow()
                });
                await db.SaveChangesAsync(cancellationToken);
                return new LlmResult<T>(cachedValue!, runId, true, cacheKey);
            }
            if (!options.UseMock && (budget.KeyRejected || string.IsNullOrWhiteSpace(options.ApiKey)
                || !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https"))
                throw new LlmException("llm_key_required");
            if (initialCount + attempt > 4)
                throw new LlmException("llm_attempt_limit");
            var keySpend = options.UseMock ? null : await client.KeySpendAsync(cancellationToken);
            await budget.HttpSlots.WaitAsync(cancellationToken);
            LlmClientResponse response;
            IReadOnlyList<string> errors = [];
            T? value = default;
            LlmSalvageResult? salvage = null;
            try
            {
                var reserve = options.UseMock ? 0 : budget.Estimate(prompt, route, request.Image);
                LlmUsageEntity entry;
                try
                {
                    entry = await budget.StartAsync(runId, request, task, route, requestKey,
                        initialCount + attempt, reserve, cancellationToken, configVersion, since, keySpend);
                }
                catch (LlmException exception) when (diagnostics is { Count: > 0 })
                {
                    throw new LlmException(exception.Code, diagnostics);
                }
                var started = Stopwatch.GetTimestamp();
                try
                {
                    response = await client.SendAsync(prompt, route, request.Image, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await budget.FinishAsync(entry, new LlmClientResponse(Error: "request_cancelled"),
                        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, null, cleanup.Token);
                    throw;
                }
                if (response.Content is { } content)
                    request.CaptureResponse?.Invoke(content);
                if (response.Error is null)
                    errors = LlmOutputValidator.Validate<T>(response.Content ?? "", prompt, request, out value, out salvage);
                await budget.FinishAsync(entry, response, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    errors.Count > 0 ? "invalid_output" : null, cancellationToken);
                if (response.Error == "llm_key_required")
                    budget.RejectKey();
                logger.LogInformation("LLM {RunId} {Stage} {Model}: {Error}, HTTP {HttpStatus}, finish {FinishReason}, diagnostics {Diagnostics}, dropped {Dropped}.",
                    runId, LlmBudget.Stage(task), route.Model, response.Error ?? (errors.Count > 0 ? "invalid_output" : "done"),
                    response.HttpStatus, response.FinishReason, string.Join(",", errors), string.Join(",", salvage?.Dropped ?? []));
            }
            finally
            {
                budget.HttpSlots.Release();
            }
            if (response.Error is null && errors.Count == 0)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(salvage?.Content ?? response.Content!);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO llm_cache (cache_key, task, model, schema_version, response_json, created_at)
                    VALUES ({cacheKey}, {LlmBudget.Stage(task)}, {route.Model}, {prompt.SchemaHash}, {json.GetRawText()}::jsonb, {timeProvider.GetUtcNow()})
                    ON CONFLICT (cache_key) DO UPDATE SET response_json = EXCLUDED.response_json, created_at = EXCLUDED.created_at
                    """, cancellationToken);
                if (task == LlmTask.Repair)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO llm_cache (cache_key, task, model, schema_version, response_json, created_at)
                        VALUES ({originalCacheKey}, {originalStage}, {options.Route(request.Task, request.Task).Model}, {prompt.SchemaHash}, {json.GetRawText()}::jsonb, {timeProvider.GetUtcNow()})
                        ON CONFLICT (cache_key) DO UPDATE SET response_json = EXCLUDED.response_json, created_at = EXCLUDED.created_at
                        """, cancellationToken);
                return new LlmResult<T>(value!, runId, false, cacheKey, salvage?.Dropped);
            }
            if (request.Task == LlmTask.Critic)
                throw new LlmException(response.Error ?? "invalid_output", errors);
            if (response.Error is not null)
            {
                if (!response.Transient || transientRetries >= 2 || attempt == 4 - initialCount || repairing)
                    throw new LlmException(response.Error);
                await Task.Delay(TimeSpan.FromSeconds(transientRetries++ == 0 ? 2 : 5), timeProvider, cancellationToken);
                continue;
            }
            if (repairing || attempt == 4 - initialCount)
                throw new LlmException("invalid_output", errors);
            repairing = true;
            previous = response.Content ?? "";
            diagnostics = errors;
            task = LlmTask.Repair;
        }
        throw new LlmException("llm_attempt_limit");
    }

    private string CacheKey(LlmRequest request, LlmTask task, LlmPrompt prompt, LlmRoute route) =>
        LlmPromptCatalog.Hash(JsonSerializer.Serialize(new
        {
            task,
            originalTask = request.Task,
            route,
            prompt.System,
            prompt.User,
            prompt.SchemaHash,
            prompt.Version,
            request.InputVersion,
            request.ValidationVersion,
            SemanticVersion = LlmSemanticChecks.Version,
            options.ConfigVersion,
            options.StructuredOutput,
            options.UseMock,
            image = request.Image is null ? null
                : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(request.Image.Bytes))
        }));
}