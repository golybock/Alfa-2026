namespace AlfaNews.Contracts.Processing;

public sealed record CriticResult(IReadOnlyList<CriticIssue> Issues);