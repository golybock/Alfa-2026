using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record AgentStepDto(
	int Ordinal,
	string Stage,
	string Title,
	string Summary,
	AgentStepStatus Status,
	long DurationMs
);