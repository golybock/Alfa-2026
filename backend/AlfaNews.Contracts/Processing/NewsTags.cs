namespace AlfaNews.Contracts.Processing;

public sealed record NewsTags(
	IReadOnlyList<string> Factors,
	IReadOnlyList<string> Okved2,
	IReadOnlyList<string> Countries,
	IReadOnlyList<string> Regions,
	IReadOnlyList<string> Commodities
);