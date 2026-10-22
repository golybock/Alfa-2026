namespace AlfaNews.Contracts.Api;

public sealed record OperatorSourcesResponse(IReadOnlyList<SourceHealthDto> Items);