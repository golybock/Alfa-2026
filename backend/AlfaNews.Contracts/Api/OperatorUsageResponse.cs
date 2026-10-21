namespace AlfaNews.Contracts.Api;

public sealed record OperatorUsageResponse(
    DateOnly MoscowDate, decimal DailyLimitRub, decimal AvailableRub,
    IReadOnlyList<StageUsageDto> Stages);