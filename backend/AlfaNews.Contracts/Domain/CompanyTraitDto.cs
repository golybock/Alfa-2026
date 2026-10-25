namespace AlfaNews.Contracts.Domain;

public sealed record CompanyTraitDto(
    string Code,
    string Label,
    decimal? AmountRub = null
);