using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record ResearchBlockDraft(
	string LocalId,
	ResearchBlockKind Kind,
	string Text,
	IReadOnlyList<TableDraft> Tables,
	IReadOnlyList<ChartDraft> Charts,
	NewsTags Tags
);