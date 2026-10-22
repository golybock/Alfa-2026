namespace AlfaNews.Contracts.Processing;

public sealed record LlmUsageEntry(
	string Id,
	string RunId,
	string? CardId,
	string Stage,
	string Model,
	int Attempt,
	long InputTokens,
	long OutputTokens,
	long? ReasoningTokens,
	decimal? ActualCostRub,
	long DurationMs,
	bool CacheHit,
	string? Error,
	string ConfigVersion,
	DateTimeOffset CreatedAt
);