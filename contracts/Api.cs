using System;
using System.Collections.Generic;

namespace AlfaNews.Contracts;

public sealed record LoginRequest(string Login, string Password);
public sealed record CompanyAccessDto(string CompanyId, string Name, IReadOnlyList<Role> Roles);
public sealed record MeResponse(string UserId, string DisplayName, bool IsOperator, IReadOnlyList<CompanyAccessDto> Companies);
public sealed record SourceSummaryDto(string Name, string Url, DateTimeOffset PublishedAt);
public sealed record FeedItemDto(
    string EventId, string Headline, string Summary, SourceSummaryDto Source,
    RelevanceDto Relevance, IReadOnlyDictionary<string, EvidenceDto> Evidence,
    AnalysisStatus AnalysisStatus, string? InsightId, string? JobId);
public sealed record FeedResponse(
    string CompanyId, DateTimeOffset AsOf, DateTimeOffset NewsPeriodFrom,
    DateTimeOffset NewsPeriodTo, DateTimeOffset? LastUpdatedAt,
    DateTimeOffset? CollectionStartedAt, RefreshStatus RefreshStatus,
    IReadOnlyList<FeedItemDto> Items);
public sealed record InsightRequest(Role Role);
public sealed record AcceptedResponse(string JobId, string StatusUrl);
public sealed record PendingInsightResponse(AnalysisStatus Status, string? JobId);
public sealed record JobResponse(
    string Id, JobStatus Status, string Stage, string? ResultType,
    string? ResultId, ApiError? Error);
public sealed record ApiError(string Code, string Message, string RequestId);
public sealed record ChatRequest(string Question);
public sealed record ChatAnswerDto(
    string Id, string InsightId, ChatAnswerStatus Status, IReadOnlyList<ClaimDto> Claims,
    IReadOnlyDictionary<string, EvidenceDto> Evidence, string? RefusalReason,
    IReadOnlyList<string> SuggestedQuestions, int QuestionsRemaining);
public sealed record ChatHistoryResponse(IReadOnlyList<ChatTurnDto> Items);
public sealed record ChatTurnDto(string Id, string Question, DateTimeOffset CreatedAt, ChatAnswerDto Answer);
public sealed record FeedbackRequest(FeedbackKind Kind, FeedbackReason? Reason);
public sealed record DocumentAcceptedResponse(string DocumentId, string JobId, ReviewStatus ReviewStatus);
public sealed record DocumentSummaryDto(
    string Id, string Title, DateOnly? PublishedOn, int PageCount, string Version,
    OcrStatus OcrStatus, ReviewStatus ReviewStatus);
public sealed record DocumentListResponse(IReadOnlyList<DocumentSummaryDto> Items);
public sealed record DocumentPageDto(
    int PageNo, OcrStatus OcrStatus, ReviewStatus ReviewStatus,
    string PageImageUrl, IReadOnlyList<ReviewableChunkDto> Chunks, string? Error);
public sealed record ReviewableChunkDto(
    string Id, ResearchBlockKind Kind, string Text, ReviewStatus ReviewStatus,
    IReadOnlyList<TableDraft> Tables, IReadOnlyList<ReviewableChartDto> Charts);
public sealed record ReviewableChartDto(ChartDraft Chart, ReviewStatus ReviewStatus);
public sealed record DocumentResponse(
    string Id, string Title, DateOnly? PublishedOn, int PageCount,
    string Version, OcrStatus OcrStatus, ReviewStatus ReviewStatus, IReadOnlyList<DocumentPageDto> Pages);
public sealed record PageReviewRequest(IReadOnlyList<ChunkReviewRequest> Chunks);
public sealed record ChunkReviewRequest(
    string ChunkId, string CorrectedText, ReviewStatus ReviewStatus,
    IReadOnlyList<TableDraft> CorrectedTables, IReadOnlyList<ChartReviewRequest> Charts, string? Note);
public sealed record ChartReviewRequest(ChartDraft CorrectedChart, ReviewStatus ReviewStatus);
public sealed record ReviewAcceptedResponse(string DocumentId, string Version, string JobId);

// Desirable after the complete MVP flow; read-only operator endpoints.
public sealed record StageUsageDto(
    string Stage, int Requests, decimal KnownCostRub, int UnknownCostRequests,
    long DurationMs, long InputTokens, long OutputTokens);
public sealed record OperatorUsageResponse(
    DateOnly MoscowDate, decimal DailyLimitRub, decimal AvailableRub,
    IReadOnlyList<StageUsageDto> Stages);
public sealed record ModelConfigResponse(
    string DeepSeekModel, string GlmModel, string GlmReasoning, int GeneralMaxTokens,
    int ChatMaxTokens, bool CriticEnabled, string CriticScope);
public sealed record SourceHealthDto(
    string Id, string Name, string FeedUrl, bool Enabled, string ValidationStatus,
    DateTimeOffset? LastSuccessAt, string? LastError);
public sealed record OperatorSourcesResponse(IReadOnlyList<SourceHealthDto> Items);
public sealed record ManualNewsRequest(
    string SourceId, string? Url, string? Title, string? Text, DateTimeOffset? PublishedAt);