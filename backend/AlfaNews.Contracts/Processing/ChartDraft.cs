namespace AlfaNews.Contracts.Processing;

public sealed record ChartDraft(
    string LocalId,
    string Title,
    string Description,
    string? XAxis,
    string? YAxis,
    string? Unit,
    string? Period,
    IReadOnlyList<string> Legend,
    IReadOnlyList<ChartPointDraft> Points,
    IReadOnlyList<decimal> Region
);