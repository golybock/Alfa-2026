using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record ChatAnswerDto(
	string Id,
	string InsightId,
	ChatAnswerStatus Status,
	IReadOnlyList<ClaimDto> Claims,
	IReadOnlyDictionary<string, EvidenceDto> Evidence,
	string? RefusalReason,
	IReadOnlyList<string> SuggestedQuestions,
	int QuestionsRemaining,
	string? Answer = null,
	IReadOnlyList<string>? Missing = null
);