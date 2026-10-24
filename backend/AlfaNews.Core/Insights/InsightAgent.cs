using System.Diagnostics;
using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlfaNews.Core.Insights;

public sealed class InsightAgent(AlfaNewsDbContext db, LlmGateway gateway, AgentTools tools, FeedRefreshService ranking,
    ResearchSearch research, RankingOptions rankingOptions, TimeProvider clock, ILogger<InsightAgent> logger)
{
    private const int MaxToolCalls = 5;
    private const int MaxPlanRounds = 2;
    private const int MaxBankSearches = 2;
    private const int BankViewMaxAgeDays = 60;
    private const int BankViewStaleMaxAgeDays = 120;

    public async Task<JobResult> GenerateAsync(BackgroundJobEntity job, GenerateInsightJob payload, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(payload.EventId, out var eventId))
            return new JobResult("rejected", ErrorCode: "invalid_payload");
        var eventRow = await db.EventRows.AsNoTracking().SingleOrDefaultAsync(row => row.Id == eventId, cancellationToken);
        if (eventRow is null)
            return new JobResult("missing", ErrorCode: "event_not_found");
        var context = await ranking.LoadContextAsync(payload.CompanyId, cancellationToken);
        if (context is null)
            return new JobResult("missing", ErrorCode: "profile_not_found");
        var roleName = AccessService.RoleName(payload.Role);
        var insight = await db.InsightRows.SingleOrDefaultAsync(row => row.CompanyId == payload.CompanyId && row.EventId == eventId
            && row.Role == roleName && row.InputVersion == payload.InputVersion, cancellationToken);
        if (insight?.Status == "ready")
            return new JobResult("ready", "insight", insight.Id.ToString());
        if (rankingOptions.InsightsOnlyForFeed && job.OwnerUserId is null && insight is null && !await InSnapshotAsync(payload, cancellationToken))
            return new JobResult("skipped");
        if (await OtherRoleRunningAsync(job, payload, cancellationToken))
            return new JobResult("deferred", Defer: true);
        var now = clock.GetUtcNow();
        if (insight is null)
        {
            insight = new InsightEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = payload.CompanyId,
                EventId = eventId,
                Role = roleName,
                InputVersion = payload.InputVersion,
                CreatedAt = now
            };
            db.InsightRows.Add(insight);
        }
        insight.Status = "processing";
        insight.LastError = null;
        insight.UpdatedAt = now;
        var run = new AgentRunEntity
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            InsightId = insight.Id,
            InputVersion = payload.InputVersion,
            Status = "running",
            StartedAt = now
        };
        db.AgentRunRows.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var body = await BuildAsync(job, payload, eventRow, context, insight, run, cancellationToken);
            insight.BodyJson = JsonSerializer.SerializeToElement(body, AgentTools.JsonOptions);
            insight.Status = "ready";
            insight.UpdatedAt = clock.GetUtcNow();
            run.Status = "done";
            run.FinishedAt = insight.UpdatedAt;
            await db.SaveChangesAsync(cancellationToken);
            return new JobResult("ready", "insight", insight.Id.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var code = exception is LlmException llm ? llm.Code : "insight_failed";
            logger.LogWarning("Карточка {InsightId}, задание {JobId}: {ErrorCode}, {ErrorType}.", insight.Id, job.Id, code, exception.GetType().Name);
            insight.Status = "failed";
            insight.LastError = code;
            insight.UpdatedAt = clock.GetUtcNow();
            run.Status = "failed";
            run.FinishedAt = insight.UpdatedAt;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await db.SaveChangesAsync(cleanup.Token);
            return new JobResult("failed", ErrorCode: code);
        }
    }

    private async Task<InsightDto> BuildAsync(BackgroundJobEntity job, GenerateInsightJob payload, EventEntity eventRow,
        CompanyRankingContext context, InsightEntity insight, AgentRunEntity run, CancellationToken cancellationToken)
    {
        var steps = new StepWriter(db, run.Id);
        var news = eventRow.ExtractionJson.Deserialize<NewsExtraction>(LlmOutputValidator.JsonOptions)!;
        // Карточка берёт дату и причины из того снимка (живого или демо), где есть событие; живой — в приоритете.
        var snapshots = await db.FeedSnapshotRows.AsNoTracking().Where(row => row.CompanyId == payload.CompanyId).ToListAsync(cancellationToken);
        var (snapshot, feedItem) = snapshots.OrderBy(row => row.Mode == FeedSettings.Live ? 0 : 1)
            .Select(row => ((FeedSnapshotEntity?)row, row.ItemsJson.Deserialize<List<FeedItemDto>>(AgentTools.JsonOptions)?.SingleOrDefault(item => item.EventId == payload.EventId)))
            .FirstOrDefault(pair => pair.Item2 is not null);
        var asOf = snapshot is not null && feedItem is not null ? snapshot.AsOf : clock.GetUtcNow();
        var ranked = await ranking.RankEventAsync(payload.CompanyId, eventRow.Id, asOf, cancellationToken)
            ?? throw new InvalidOperationException("Событие недоступно для ранжирования.");
        var relevance = feedItem?.Relevance ?? new RelevanceDto(ranked.Score, 0, ranked.Reasons, ranked.Contributions);
        relevance = relevance with { Significance = FeedRanking.SignificanceOf(relevance) };
        var rankingResult = new RankingResult(payload.CompanyId, payload.EventId, ranked.Base, ranked.Recency, ranked.Candidate.SourceReliability,
            ranked.Score, relevance.Rank, ranked.Eligible, rankingOptions.ConfigVersion, ranked.Contributions);
        var registry = new EvidenceRegistry(feedItem?.Evidence ?? ranked.Evidence);
        var scope = new AgentScope(context.Company, news, eventRow.Id, asOf);
        var outline = news with { Facts = [] };
        var limitations = new List<string>();
        var notes = new List<string>();
        string ContextJson(Role? role, IReadOnlySet<string>? only = null) => JsonSerializer.Serialize(new AgentContext(role, asOf,
            payload.InputVersion, context.Company, outline, rankingResult, AgentTools.ForContext(registry.Items, only),
            Unique(limitations.Concat(notes))), AgentTools.JsonOptions);

        var executed = new List<ToolExecution>();
        var bankSearches = 0;
        for (var round = 1; round <= MaxPlanRounds && executed.Count < MaxToolCalls; round++)
        {
            var contextJson = ContextJson(null);
            var previousJson = JsonSerializer.Serialize(executed.Select(item => new
            {
                name = item.Name,
                arguments = Canonical(item.Arguments),
                evidence_ids = item.EvidenceIds,
                limitations = item.Limitations,
                summary = item.Summary
            }));
            var permitted = AgentTools.Definitions.Where(pair => pair.Key != AgentTools.BankSearch || bankSearches < MaxBankSearches)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var started = Stopwatch.GetTimestamp();
            var plan = (await gateway.GenerateAsync<AgentPlan>(new LlmRequest(job.Id, $"insight:{insight.Id}:plan:{round}",
                payload.InputVersion, LlmTask.Plan, new Dictionary<string, JsonElement>
                {
                    ["CONTEXT_JSON"] = JsonSerializer.Deserialize<JsonElement>(contextJson),
                    ["PREVIOUS_TOOL_RESULTS_JSON"] = JsonSerializer.Deserialize<JsonElement>(previousJson)
                }, insight.Id, Runtime: new LlmRuntime(permitted, MaxToolCalls - executed.Count), RunId: run.Id), cancellationToken)).Value;
            await steps.AddAsync("plan", null, null, JsonSerializer.SerializeToElement(plan, AgentTools.JsonOptions), [],
                plan.QuestionsToResolve.Count > 0 ? string.Join(" ", plan.QuestionsToResolve) : "Дополнительные вопросы не заданы.", started, cancellationToken);
            var fresh = plan.Calls.Select(call => call with { Arguments = Canonical(call.Arguments) })
                .Where(call => permitted.ContainsKey(call.Name)
                    && !executed.Any(item => item.Name == call.Name && item.Arguments.GetRawText() == call.Arguments.GetRawText()))
                .Take(MaxToolCalls - executed.Count).ToArray();
            if (fresh.Length == 0)
                break;
            foreach (var call in fresh)
            {
                if (call.Name == AgentTools.BankSearch && bankSearches++ >= MaxBankSearches)
                    continue;
                executed.Add(await RunToolAsync(call, "tool", scope, registry, limitations, steps, cancellationToken));
            }
        }
        if (executed.All(item => item.Name != AgentTools.EventTool))
            executed.Add(await RunToolAsync(new ToolCall(AgentTools.EventTool, JsonSerializer.SerializeToElement(new { })), "default_event",
                scope, registry, limitations, steps, cancellationToken));
        if (bankSearches == 0)
            executed.Add(await RunToolAsync(new ToolCall(AgentTools.BankSearch, JsonSerializer.SerializeToElement(new
            {
                query = news.Headline[..Math.Min(news.Headline.Length, 500)],
                factors = news.Tags.Factors,
                limit = 4
            })), "default_search", scope, registry, limitations, steps, cancellationToken));

        var eventDate = eventRow.EventDate ?? DateOnly.FromDateTime(eventRow.PublishedAt.ToOffset(TimeSpan.FromHours(3)).DateTime);
        var (applicable, dated) = await ApplicableBankEvidenceAsync(registry.Items, news, eventDate,
            DateOnly.FromDateTime(asOf.ToOffset(TimeSpan.FromHours(3)).DateTime), cancellationToken);
        var inapplicable = registry.Items.Where(pair => pair.Value.Type == EvidenceType.BankResearch && !applicable.Contains(pair.Key))
            .Select(pair => pair.Key).ToArray();
        if (inapplicable.Length > 0)
            notes.Add($"Не являются позицией банка по этому событию: {string.Join(", ", inapplicable)}. У них нет даты публикации, дата старше {BankViewStaleMaxAgeDays} дней до события, тема не совпадает с факторами события или по теме есть более поздний обзор банка. Не ссылайся на них ни в одном утверждении: bank_view — not_found, а в limitations одной строкой укажи дату и тему обзора и почему он не относится к событию.");
        if (dated.Count > 0)
            notes.Add("DATED: " + string.Join("; ", dated.Select(pair => $"{pair.Key} — оценка банка на {pair.Value:dd.MM.yyyy}")) +
                $". Обзоры на тему события, старше {BankViewMaxAgeDays} дней, более поздних обзоров банка по теме нет. Их можно показать в bank_view только как оценку банка на эту дату: в утверждении назови дату («по оценке банка на 11.09.2026») и что более поздних оценок по теме нет; это не реакция банка на событие.");
        var finalContext = ContextJson(payload.Role);
        var evidence = registry.Items;
        var draftRequest = new LlmRequest(job.Id, $"insight:{insight.Id}:draft", payload.InputVersion, LlmTask.Draft,
            new Dictionary<string, JsonElement> { ["CONTEXT_JSON"] = JsonSerializer.Deserialize<JsonElement>(finalContext) }, insight.Id,
            ValidateOutput: json => InsightChecks.Validate(json, evidence, applicable, dated), ValidationVersion: InsightChecks.Version,
            Runtime: new LlmRuntime(Rank: rankingResult.Rank), RunId: run.Id);
        var draftStarted = Stopwatch.GetTimestamp();
        var drafted = await gateway.GenerateAsync<InsightDraft>(draftRequest, cancellationToken);
        var draft = InsightChecks.Trim(drafted.Value);
        await steps.AddAsync("draft", null, null, null, draft.Claims.SelectMany(claim => claim.Refs).Distinct().ToArray(),
            "Черновик карточки прошёл проверки кодом." + DroppedNote(drafted.Dropped), draftStarted, cancellationToken);
        var draftJson = JsonSerializer.Serialize(draft, AgentTools.JsonOptions);
        var criticContext = ContextJson(payload.Role, draft.Claims.SelectMany(claim => claim.Refs).ToHashSet(StringComparer.Ordinal));

        IReadOnlyList<CriticIssue> issues = [];
        CriticStatus criticStatus;
        string? criticReason = null;
        string? criticError = null;
        var criticStarted = Stopwatch.GetTimestamp();
        try
        {
            issues = (await gateway.GenerateAsync<CriticResult>(new LlmRequest(job.Id, $"insight:{insight.Id}:critic",
                payload.InputVersion, LlmTask.Critic, new Dictionary<string, JsonElement>
                {
                    ["CONTEXT_JSON"] = JsonSerializer.Deserialize<JsonElement>(criticContext),
                    ["DRAFT_JSON"] = JsonSerializer.Deserialize<JsonElement>(draftJson)
                }, insight.Id, Runtime: new LlmRuntime(Rank: rankingResult.Rank), RunId: run.Id), cancellationToken)).Value.Issues;
            criticStatus = issues.Count == 0 ? CriticStatus.Passed : CriticStatus.IssuesFound;
        }
        catch (LlmException exception)
        {
            criticError = exception.Code;
            (criticStatus, criticReason) = exception.Code switch
            {
                "critic_disabled" => (CriticStatus.SkippedDisabled, "Проверка критиком отключена настройкой."),
                "budget_exhausted" or "budget_unknown_cost" => (CriticStatus.SkippedBudget, "Недостаточно дневного бюджета модели для проверки критиком."),
                "critic_already_attempted" => (CriticStatus.SkippedError, "Критик уже вызывался для этой версии карточки; повторный запрос не выполняется."),
                _ => (CriticStatus.SkippedError, "Критик не ответил корректно; повторный запрос не выполняется.")
            };
        }
        await steps.AddAsync("critic", null, null, JsonSerializer.SerializeToElement(issues, AgentTools.JsonOptions),
            issues.Select(issue => issue.ClaimId).Distinct().ToArray(), criticError is null ? $"Замечаний критика: {issues.Count}." : criticReason + " Код: " + criticError + ".",
            criticStarted, cancellationToken);

        var original = draft.Claims.ToDictionary(claim => claim.Id, claim => claim.Text, StringComparer.Ordinal);
        var repaired = false;
        if (criticStatus == CriticStatus.IssuesFound && await RepairCountAsync(run.Id, cancellationToken) == 0)
        {
            var repairStarted = Stopwatch.GetTimestamp();
            try
            {
                var fixedDraft = await gateway.RepairAsync<InsightDraft>(draftRequest, draftJson,
                    issues.Select(issue => $"critic:{issue.ClaimId}:{issue.Code}: {issue.Explanation}").ToArray(), cancellationToken);
                draft = InsightChecks.Trim(fixedDraft.Value);
                repaired = true;
                await steps.AddAsync("repair", null, null, null, draft.Claims.SelectMany(claim => claim.Refs).Distinct().ToArray(),
                    "Черновик исправлен по замечаниям критика и повторно прошёл проверки кодом." + DroppedNote(fixedDraft.Dropped),
                    repairStarted, cancellationToken);
            }
            catch (LlmException exception)
            {
                await steps.AddAsync("repair", null, null, null, [], "Починка не прошла: " + exception.Code + ". Сохранена последняя корректная версия.",
                    repairStarted, cancellationToken);
            }
        }
        var issueIds = issues.Select(issue => issue.ClaimId).ToHashSet(StringComparer.Ordinal);
        var claims = draft.Claims.Select(claim =>
        {
            var unchanged = original.TryGetValue(claim.Id, out var text) && text == claim.Text;
            var status = criticStatus switch
            {
                CriticStatus.Passed => ClaimVerification.CriticPassed,
                CriticStatus.IssuesFound when issueIds.Contains(claim.Id) && (!repaired || unchanged) => ClaimVerification.NotConfirmed,
                CriticStatus.IssuesFound when unchanged && !issueIds.Contains(claim.Id) => ClaimVerification.CriticPassed,
                _ => ClaimVerification.CodePassed
            };
            return new ClaimDto(claim.Id, claim.Text, claim.Origin, claim.Refs, status, claim.Confidence);
        }).ToArray();
        var unconfirmed = claims.Where(claim => claim.VerificationStatus == ClaimVerification.NotConfirmed).Select(claim => claim.Id)
            .ToHashSet(StringComparer.Ordinal);
        // На экране не больше трёх ограничений: сначала отметка о неподтверждённых выводах, затем ограничения модели и инструментов.
        string[] critical = unconfirmed.Count > 0 ? ["Часть выводов не подтверждена проверкой критика и показана с отметкой «не подтверждено»."] : [];
        var shownLimitations = Unique(critical.Concat(draft.Limitations).Concat(limitations)).Take(InsightChecks.MaxLimitations).ToArray();
        var requestCount = await db.LlmUsageRows.AsNoTracking().CountAsync(row => row.RunId == run.Id && row.Stage == "critic" && row.Attempt > 0,
            cancellationToken);
        return new InsightDto(insight.Id.ToString(), payload.EventId, payload.CompanyId, payload.Role, asOf, draft.Headline, relevance,
            draft.WhatHappened, draft.WhyItMatters, draft.Consequences, draft.Actions.Where(id => !unconfirmed.Contains(id)).ToArray(),
            draft.BankView, claims, registry.Items, shownLimitations,
            draft.SuggestedQuestions.Take(3).ToArray(), run.Id.ToString(),
            new VerificationDto(CodeStatus.Passed, new CriticVerificationDto(criticStatus, requestCount, criticReason),
                Math.Min(1, await RepairCountAsync(run.Id, cancellationToken))));
    }

    // Позиция банка: обзор на тему события не старше 60 дней; обзор 60–120 дней — только как датированная оценка и только
    // если более позднего обзора банка по этой теме нет (иначе оценка устарела).
    private async Task<(IReadOnlySet<string> Applicable, IReadOnlyDictionary<string, DateOnly> Dated)> ApplicableBankEvidenceAsync(
        IReadOnlyDictionary<string, EvidenceDto> evidence, NewsExtraction news, DateOnly eventDate, DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var candidates = evidence.Where(pair => pair.Value is { Type: EvidenceType.BankResearch, Date: { } date }
                && date >= eventDate.AddDays(-BankViewStaleMaxAgeDays) && date <= asOf && Guid.TryParse(pair.Value.ChunkId, out _))
            .ToDictionary(pair => pair.Key, pair => (Chunk: Guid.Parse(pair.Value.ChunkId!), Date: pair.Value.Date!.Value), StringComparer.Ordinal);
        var matching = await research.MatchingFactorsAsync(candidates.Values.Select(item => item.Chunk).ToArray(), news.Tags.Factors, cancellationToken);
        var applicable = new HashSet<string>(StringComparer.Ordinal);
        var dated = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        foreach (var (id, item) in candidates.Where(pair => matching.Contains(pair.Value.Chunk)))
        {
            if (item.Date >= eventDate.AddDays(-BankViewMaxAgeDays))
                applicable.Add(id);
            else if (!await research.LaterOnTopicAsync(item.Date, asOf, news.Tags.Factors, cancellationToken))
            {
                applicable.Add(id);
                dated[id] = item.Date;
            }
        }
        return (applicable, dated);
    }

    private static JsonElement Canonical(JsonElement value) => JsonSerializer.SerializeToElement(Sorted(value));

    private static object? Sorted(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => Sorted(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(Sorted).ToArray(),
        _ => value
    };

    private static string[] Unique(IEnumerable<string> items) => items.Where(item => !string.IsNullOrWhiteSpace(item))
        .DistinctBy(item => string.Join(' ', item.ToLowerInvariant().Replace('ё', 'е').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.', ';', ' '), StringComparer.Ordinal).ToArray();

    private async Task<ToolExecution> RunToolAsync(ToolCall call, string stage, AgentScope scope, EvidenceRegistry registry,
        List<string> limitations, StepWriter steps, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await tools.ExecuteAsync(call, scope, registry, cancellationToken);
        limitations.AddRange(result.Limitations);
        await steps.AddAsync(stage, call.Name, call.Arguments, JsonSerializer.SerializeToElement(new
        {
            evidence_ids = result.EvidenceIds,
            limitations = result.Limitations
        }), result.EvidenceIds, result.Summary, started, cancellationToken);
        return result;
    }

    // План общий для CEO и CFO: пока идёт карточка другой роли по тому же событию, ждём её и берём план из кэша шлюза.
    // Ждёт задание, захваченное позже (при равенстве — с большим ID), чтобы две роли не откладывали друг друга по кругу.
    // Автоматическая карточка нужна, только пока событие в ленте компании: пока задание ждало, его могли вытеснить
    // более свежие события (лента — не больше пяти). Карточку, которую открыл пользователь, делаем всегда.
    private async Task<bool> InSnapshotAsync(GenerateInsightJob payload, CancellationToken cancellationToken)
    {
        var snapshots = await db.FeedSnapshotRows.AsNoTracking().Where(row => row.CompanyId == payload.CompanyId)
            .Select(row => row.ItemsJson).ToListAsync(cancellationToken);
        return snapshots.Any(items => items.Deserialize<List<FeedItemDto>>(AgentTools.JsonOptions)?.Any(item => item.EventId == payload.EventId) == true);
    }

    private async Task<bool> OtherRoleRunningAsync(BackgroundJobEntity job, GenerateInsightJob payload, CancellationToken cancellationToken)
    {
        var kind = JobKinds.Name<GenerateInsightJob>();
        var running = await db.BackgroundJobRows.AsNoTracking()
            .Where(row => row.Kind == kind && row.Status == "running" && row.Id != job.Id && row.CompanyId == payload.CompanyId)
            .Select(row => new { row.Id, row.StartedAt, row.Payload }).ToListAsync(cancellationToken);
        return running.Any(other => other.Payload.Deserialize<GenerateInsightJob>(JobKinds.JsonOptions) is { } peer
            && peer.EventId == payload.EventId && peer.InputVersion == payload.InputVersion && peer.Role != payload.Role
            && (other.StartedAt < job.StartedAt || other.StartedAt == job.StartedAt && other.Id.CompareTo(job.Id) < 0));
    }

    private static string DroppedNote(IReadOnlyList<string>? dropped) =>
        LlmSalvage.Describe(dropped ?? []) is { Length: > 0 } reasons ? $" Код убрал утверждения с ошибками: {reasons}." : "";

    private Task<int> RepairCountAsync(Guid runId, CancellationToken cancellationToken) => db.LlmUsageRows.AsNoTracking()
        .CountAsync(row => row.RunId == runId && row.Stage == "repair" && row.Attempt > 0, cancellationToken);

    private sealed class StepWriter(AlfaNewsDbContext db, Guid runId)
    {
        private int ordinal;

        public async Task AddAsync(string stage, string? toolName, JsonElement? arguments, JsonElement? result, IReadOnlyList<string> refs,
            string summary, long started, CancellationToken cancellationToken)
        {
            db.AgentStepRows.Add(new AgentStepEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                Ordinal = ++ordinal,
                Stage = stage,
                ToolName = toolName,
                ArgumentsJson = arguments,
                ResultJson = result,
                EvidenceRefs = JsonSerializer.SerializeToElement(refs),
                Summary = summary,
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}