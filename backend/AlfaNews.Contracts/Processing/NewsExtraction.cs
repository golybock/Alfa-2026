namespace AlfaNews.Contracts.Processing;

public sealed record NewsExtraction(
	string Headline,
	DateOnly? EventDate,
	IReadOnlyList<ExtractedFact> Facts,
	IReadOnlyList<string> Participants,
	NewsTags Tags
);