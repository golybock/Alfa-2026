namespace AlfaNews.Contracts.Api;

public sealed record InsightStepsResponse(
	string InsightId,
	long TotalDurationMs,
	IReadOnlyList<AgentStepDto> Steps
);