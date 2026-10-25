namespace AlfaNews.Contracts.Api;

public sealed record ManualNewsRequest(
    string? SourceId = null,
    string? Url = null,
    string? Title = null,
    string? Text = null,
    DateTimeOffset? PublishedAt = null);