using System.Text;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlfaNews.Core.Llm;

public sealed class LlmBudget(IServiceScopeFactory scopes, TimeProvider timeProvider, LlmOptions options) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int keyRejected;
    public SemaphoreSlim HttpSlots { get; } = new(2, 2);
    public bool KeyRejected => Volatile.Read(ref keyRejected) != 0;
    public void RejectKey() => Interlocked.Exchange(ref keyRejected, 1);

    public static DateTimeOffset DayStart(DateTimeOffset now) =>
        new(now.ToOffset(TimeSpan.FromHours(3)).AddHours(-3).Date.AddHours(3), TimeSpan.FromHours(3));

    public decimal Estimate(LlmPrompt prompt, LlmRoute route, LlmImage? image)
    {
        var bytes = Encoding.UTF8.GetByteCount(prompt.System) + Encoding.UTF8.GetByteCount(prompt.User);
        if (options.StructuredOutput)
            bytes += Encoding.UTF8.GetByteCount(prompt.Schema);
        if (bytes > options.MaxInputBytes || image is { Bytes.Length: > 16777216 })
            throw new LlmException("llm_input_too_large");
        if (route.InputRubPerMillion <= 0 || route.OutputRubPerMillion <= 0)
            throw new LlmException("llm_reserve_configuration_required");
        var input = bytes + 1024L + (image is null ? 0 : options.ImageInputTokenReserve);
        return decimal.Ceiling((input * route.InputRubPerMillion + route.MaxOutputTokens * route.OutputRubPerMillion) / 10000m) / 100m;
    }

    public async Task<LlmUsageEntity> StartAsync(Guid runId, LlmRequest request, LlmTask task, LlmRoute route,
        string requestKey, int attempt, decimal reserve, CancellationToken cancellationToken, string? configVersion = null)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
            var stage = Stage(task);
            var originalStage = Stage(request.Task);
            if (await db.LlmUsageRows.CountAsync(row => row.RequestKey == requestKey && row.Attempt > 0
                    && (row.Stage == originalStage || row.Stage == "repair"), cancellationToken) >= 4)
                throw new LlmException("llm_attempt_limit");
            if (task is LlmTask.Critic or LlmTask.Repair
                && await db.LlmUsageRows.AnyAsync(row => row.RequestKey == requestKey && row.Stage == stage && row.Attempt > 0, cancellationToken))
                throw new LlmException(task == LlmTask.Critic ? "critic_already_attempted" : "repair_already_used");
            var now = timeProvider.GetUtcNow();
            if (reserve > 0)
            {
                var start = DayStart(now).ToUniversalTime();
                var overlap = start.AddSeconds(-Math.Max(options.TimeoutSeconds, options.CriticTimeoutSeconds));
                var balance = await db.Database.SqlQuery<BudgetBalance>($"""
                    SELECT COALESCE(SUM(COALESCE(actual_cost_rub, reserved_cost_rub)), 0) AS "CommittedRub",
                           COALESCE(bool_or(actual_cost_rub IS NULL AND reserved_cost_rub = 0 AND NOT cache_hit), false) AS "HasUnknownCost"
                    FROM llm_usage
                    WHERE created_at >= {start}
                       OR created_at + duration_ms * interval '1 millisecond' >= {start}
                       OR (error = 'request_started' AND created_at >= {overlap})
                    """).SingleAsync(cancellationToken);
                if (balance.CommittedRub + reserve > options.DailyLimitRub)
                    throw new LlmException("budget_exhausted");
                if (balance.HasUnknownCost)
                    throw new LlmException("budget_unknown_cost");
            }
            var entry = new LlmUsageEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                CardId = request.CardId,
                Stage = stage,
                Model = route.Model,
                ConfigVersion = configVersion ?? options.ConfigVersion,
                Attempt = attempt,
                ReservedCostRub = reserve,
                RequestKey = requestKey,
                CreatedAt = now,
                Error = "request_started"
            };
            db.LlmUsageRows.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
            return entry;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task FinishAsync(LlmUsageEntity entry, LlmClientResponse response, long durationMs,
        string? validationError, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>();
            entry.InputTokens = response.InputTokens;
            entry.OutputTokens = response.OutputTokens;
            entry.ReasoningTokens = response.ReasoningTokens;
            entry.ActualCostRub = response.ActualCostRub;
            entry.DurationMs = durationMs;
            entry.Error = response.Error ?? validationError;
            db.LlmUsageRows.Update(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public static string Stage(LlmTask task) => task switch
    {
        LlmTask.NewsExtract => "news_extract",
        LlmTask.PdfOcr => "pdf_ocr",
        LlmTask.ScopeGate => "scope_gate",
        _ => task.ToString().ToLowerInvariant()
    };

    public void Dispose()
    {
        gate.Dispose();
        HttpSlots.Dispose();
    }

    private sealed record BudgetBalance(decimal CommittedRub, bool HasUnknownCost);
}