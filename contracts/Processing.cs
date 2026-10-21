using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlfaNews.Contracts;

public sealed record SourceConfig(
    string Id, string Name, string FeedUrl, string Kind, IReadOnlyList<string> AllowedHosts,
    bool Enabled, int PollSeconds, decimal Reliability, string ValidationStatus);
public sealed record CollectedNews(
    string SourceId, string CanonicalUrl, string Title, string Text,
    DateTimeOffset? PublishedAt, DateTimeOffset FetchedAt, string TextSha256);
public sealed record NewsExtraction(
    string Headline, DateOnly? EventDate, IReadOnlyList<ExtractedFact> Facts,
    IReadOnlyList<string> Participants, NewsTags Tags);
public sealed record ExtractedFact(string Text, string Quote);
public sealed record NewsTags(
    IReadOnlyList<string> Factors, IReadOnlyList<string> Okved2,
    IReadOnlyList<string> Countries, IReadOnlyList<string> Regions,
    IReadOnlyList<string> Commodities);
public sealed record PdfPageExtraction(IReadOnlyList<ResearchBlockDraft> Blocks);
public sealed record ResearchBlockDraft(
    string LocalId, ResearchBlockKind Kind, string Text, IReadOnlyList<TableDraft> Tables,
    IReadOnlyList<ChartDraft> Charts, NewsTags Tags);
public sealed record TableDraft(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);
public sealed record ChartDraft(
    string LocalId, string Title, string Description, string? XAxis, string? YAxis,
    string? Unit, string? Period, IReadOnlyList<string> Legend,
    IReadOnlyList<ChartPointDraft> Points, IReadOnlyList<decimal> Region);
public sealed record ChartPointDraft(
    string Series, string Period, decimal? Value, decimal? Low, decimal? High,
    string Unit, bool Approximate, string ReadingMethod);
public sealed record BankDocumentIndexEntry(
    string FilePath, string Sha256, string? Title, DateOnly? PublishedOn,
    string? SourceUrl, int PageCount, string TextLayer, ReviewStatus ReviewStatus);

public sealed record ImportCsvJob(string CompanyFile, string PaymentFile, string DatasetVersion);
public sealed record RebuildProfilesJob(string DatasetVersion);
public sealed record CollectNewsJob(IReadOnlyList<string> SourceIds);
public sealed record ParseNewsJob(string NewsRawId);
public sealed record RefreshFeedJob(string CompanyId, DateTimeOffset AsOf);
public sealed record IndexPdfJob(string DocumentId);
public sealed record OcrPdfPageJob(string DocumentId, int PageNo);
public sealed record FinalizePdfJob(string DocumentId);
public sealed record EmbedContentJob(string EntityType, string EntityId, string Version);
public sealed record GenerateInsightJob(string CompanyId, string EventId, Role Role, string InputVersion);
public sealed record AnswerChatJob(string UserId, string InsightId, string Question, string ChatTurnId);
public sealed record PrecomputeDemoJob(IReadOnlyList<string> CompanyIds, DateTimeOffset AsOf);
public sealed record CollectManualNewsJob(ManualNewsRequest Request);

public sealed record AgentPlan(IReadOnlyList<ToolCall> Calls, IReadOnlyList<string> QuestionsToResolve);
public sealed record AgentContext(
    Role Role, DateTimeOffset AsOf, string InputVersion,
    CompanyProfile Company, NewsExtraction News, RankingResult Ranking,
    IReadOnlyDictionary<string, EvidenceDto> Evidence, IReadOnlyList<string> Limitations);
public sealed record ToolCall(string Name, JsonElement Arguments);
public sealed record ToolResult(IReadOnlyDictionary<string, EvidenceDto> Evidence, IReadOnlyList<string> Limitations);
public sealed record DraftClaim(string Id, string Text, Origin Origin, IReadOnlyList<string> Refs, Confidence? Confidence);
public sealed record InsightDraft(
    string Headline, IReadOnlyList<string> WhatHappened, IReadOnlyList<string> WhyItMatters,
    IReadOnlyList<ConsequenceDto> Consequences, IReadOnlyList<string> Actions,
    BankViewDto BankView, IReadOnlyList<DraftClaim> Claims,
    IReadOnlyList<string> Limitations, IReadOnlyList<string> SuggestedQuestions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ForecastDto? Forecast = null);
public sealed record ChatDraft(IReadOnlyList<DraftClaim> Claims, IReadOnlyList<string> SuggestedQuestions);
public sealed record SearchResearchArguments(string Query, IReadOnlyList<string> Factors, int Limit);
public sealed record GetPaymentArguments(PaymentDirection Direction, string? Okved2);
public enum LlmTask { NewsExtract, Plan, Draft, Repair, PdfOcr, Critic, Chat, ScopeGate }
public sealed record CriticIssue(string ClaimId, string Code, string Explanation);
public sealed record CriticResult(IReadOnlyList<CriticIssue> Issues);
public sealed record ScopeGateResult(ScopeLabel Label, string Reason);
public sealed record LlmUsageEntry(
    string Id, string RunId, string? CardId, string Stage, string Model,
    int Attempt, long InputTokens, long OutputTokens, long? ReasoningTokens,
    decimal? ActualCostRub, long DurationMs, bool CacheHit, string? Error,
    string ConfigVersion, DateTimeOffset CreatedAt);