namespace AlfaNews.Contracts.Domain;

public sealed record RelevanceDto(
    decimal Score,
    int Rank,
    IReadOnlyList<RelevanceReasonDto> Reasons,
    IReadOnlyList<FeatureContribution> Contributions,
    Significance? Significance = null
);