namespace AlfaNews.Contracts.Domain;

public sealed record ClaimDto(
	string Id,
	string Text,
	Origin Origin,
	IReadOnlyList<string> Refs,
	ClaimVerification VerificationStatus,
	Confidence? Confidence
);