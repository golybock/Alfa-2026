namespace AlfaNews.Contracts.Domain;

public sealed record ForecastInputDto(
	string Name,
	decimal Value,
	string Unit,
	bool Assumed,
	IReadOnlyList<string> Refs
);