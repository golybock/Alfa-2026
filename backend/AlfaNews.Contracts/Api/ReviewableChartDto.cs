using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Contracts.Api;

public sealed record ReviewableChartDto(ChartDraft Chart, ReviewStatus ReviewStatus);