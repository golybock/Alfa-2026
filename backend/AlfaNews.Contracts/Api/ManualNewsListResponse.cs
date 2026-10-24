using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record ManualNewsItemDto(
    string JobId,
    string Url,
    string? Title,
    JobStatus Status,
    string Stage,
    ApiError? Error,
    DateTimeOffset CreatedAt
);

public sealed record ManualNewsListResponse(IReadOnlyList<ManualNewsItemDto> Items);