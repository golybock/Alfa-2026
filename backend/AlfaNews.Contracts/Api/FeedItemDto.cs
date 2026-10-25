using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record FeedItemDto(
    string EventId,
    string Headline,
    string Summary,
    SourceSummaryDto Source,
    RelevanceDto Relevance,
    IReadOnlyDictionary<string, EvidenceDto> Evidence,
    AnalysisStatus AnalysisStatus,
    string? InsightId,
    string? JobId,
    string? InsightHeadline = null,
    ConsequenceType? Direction = null,
    DateTimeOffset? QueuedAt = null,
    bool RetryAvailable = false,
    string? InsightWhyYou = null
);