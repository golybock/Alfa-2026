namespace AlfaNews.Contracts.Domain;

public sealed record RankingResult(
	string CompanyId,
	string EventId,
	decimal Base,
	decimal Recency,
	decimal SourceReliability,
	bool HasBankView,
	decimal Score,
	int Rank,
	bool Eligible,
	string ConfigVersion,
	IReadOnlyList<FeatureContribution> Features
);