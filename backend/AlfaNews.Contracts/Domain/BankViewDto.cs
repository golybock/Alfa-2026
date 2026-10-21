namespace AlfaNews.Contracts.Domain;

public sealed record BankViewDto(
	BankViewStatus Status,
	IReadOnlyList<string> ClaimIds
);