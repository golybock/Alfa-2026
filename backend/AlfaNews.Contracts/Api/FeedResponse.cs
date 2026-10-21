namespace AlfaNews.Contracts.Api;

public sealed record FeedResponse(
    string CompanyId, DateTimeOffset AsOf, DateTimeOffset NewsPeriodFrom,
    DateTimeOffset NewsPeriodTo, DateTimeOffset? LastUpdatedAt,
    DateTimeOffset? CollectionStartedAt, RefreshStatus RefreshStatus,
    IReadOnlyList<FeedItemDto> Items);