namespace AlfaNews.Contracts.Api;

public sealed record ReviewableChartDto(ChartDraft Chart, ReviewStatus ReviewStatus);