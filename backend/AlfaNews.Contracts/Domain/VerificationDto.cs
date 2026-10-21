namespace AlfaNews.Contracts.Domain;

public sealed record VerificationDto(
	CodeStatus CodeStatus,
	CriticVerificationDto Critic,
	int RepairCount
);