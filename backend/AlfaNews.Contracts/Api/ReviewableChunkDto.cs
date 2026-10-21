namespace AlfaNews.Contracts.Api;

public sealed record ReviewableChunkDto(
    string Id, ResearchBlockKind Kind, string Text, ReviewStatus ReviewStatus,
    IReadOnlyList<TableDraft> Tables, IReadOnlyList<ReviewableChartDto> Charts);