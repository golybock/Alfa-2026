namespace AlfaNews.Contracts.Domain;

public sealed record FeatureContribution(
	RankingFeatureCode Code,
	decimal Value,
	decimal Weight,
	decimal Contribution,
	bool Known,
	IReadOnlyList<string> EvidenceRefs
);