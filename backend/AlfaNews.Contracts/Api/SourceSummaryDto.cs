namespace AlfaNews.Contracts.Api;

public sealed record SourceSummaryDto(string Name, string Url, DateTimeOffset PublishedAt);