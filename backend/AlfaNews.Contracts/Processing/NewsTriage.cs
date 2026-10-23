namespace AlfaNews.Contracts.Processing;

public sealed record NewsTriage(IReadOnlyList<NewsImportanceDecision> Decisions);