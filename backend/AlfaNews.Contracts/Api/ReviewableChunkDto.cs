using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Contracts.Api;

public sealed record ReviewableChunkDto(
	string Id,
	ResearchBlockKind Kind,
	string Text,
	ReviewStatus ReviewStatus,
	IReadOnlyList<TableDraft> Tables,
	IReadOnlyList<ReviewableChartDto> Charts
);