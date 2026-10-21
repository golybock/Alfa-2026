namespace AlfaNews.Contracts.Api;

public sealed record ChartReviewRequest(ChartDraft CorrectedChart, ReviewStatus ReviewStatus);