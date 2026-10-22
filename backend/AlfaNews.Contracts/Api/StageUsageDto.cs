namespace AlfaNews.Contracts.Api;

public sealed record StageUsageDto(
    string Stage, int Requests, decimal KnownCostRub, int UnknownCostRequests,
    long DurationMs, long InputTokens, long OutputTokens);