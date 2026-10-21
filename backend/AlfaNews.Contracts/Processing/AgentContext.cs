using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record AgentContext(
	Role Role,
	DateTimeOffset AsOf,
	string InputVersion,
	CompanyProfile Company,
	NewsExtraction News,
	RankingResult Ranking,
	IReadOnlyDictionary<string, EvidenceDto> Evidence,
	IReadOnlyList<string> Limitations
);