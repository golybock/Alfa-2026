namespace AlfaNews.Contracts.Api;

public sealed record SourceHealthDto(
    string Id, string Name, string FeedUrl, bool Enabled, string ValidationStatus,
    DateTimeOffset? LastSuccessAt, string? LastError);