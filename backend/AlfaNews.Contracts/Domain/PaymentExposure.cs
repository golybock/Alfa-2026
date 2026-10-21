namespace AlfaNews.Contracts.Domain;

public sealed record PaymentExposure(
	decimal TotalExternalAmount,
	decimal KnownSectorAmount,
	decimal Coverage,
	IReadOnlyDictionary<string, decimal> SectorShares
);