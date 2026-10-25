namespace AlfaNews.Contracts.Domain;

public sealed record ForecastDto(
    ForecastStatus Status,
    string Metric,
    string Unit,
    decimal? Low,
    decimal? High,
    string? Formula,
    IReadOnlyList<ForecastInputDto> Inputs,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> MissingData,
    Origin Origin
);