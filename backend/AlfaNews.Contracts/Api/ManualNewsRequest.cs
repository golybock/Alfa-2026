namespace AlfaNews.Contracts.Api;

public sealed record ManualNewsRequest(
    string SourceId, string? Url, string? Title, string? Text, DateTimeOffset? PublishedAt);