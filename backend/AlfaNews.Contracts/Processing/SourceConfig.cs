namespace AlfaNews.Contracts.Processing;

public sealed record SourceConfig(
    string Id,
    string Name,
    string FeedUrl,
    string Kind,
    IReadOnlyList<string> AllowedHosts,
    bool Enabled,
    int PollSeconds,
    decimal Reliability,
    string ValidationStatus,
    bool IsSpecialized = false
);