namespace AlfaNews.Contracts.Api;

public sealed record NewsSourceDto(string Id, string Name, string FeedUrl, bool IsSpecialized);

public sealed record NewsSourcesResponse(IReadOnlyList<NewsSourceDto> Items);