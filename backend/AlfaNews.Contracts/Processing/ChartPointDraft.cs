namespace AlfaNews.Contracts.Processing;

public sealed record ChartPointDraft(
    string Series,
    string Period,
    decimal? Value,
    decimal? Low,
    decimal? High,
    string Unit,
    bool Approximate,
    string ReadingMethod
);