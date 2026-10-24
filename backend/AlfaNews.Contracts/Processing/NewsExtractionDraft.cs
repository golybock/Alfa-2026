namespace AlfaNews.Contracts.Processing;

public sealed record NewsExtractionDraft(
	string Headline,
	DateOnly? EventDate,
	IReadOnlyList<FactReference> Facts,
	IReadOnlyList<string> Participants,
	NewsTags Tags,
	bool Commentary
);