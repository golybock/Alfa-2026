namespace AlfaNews.Contracts.Api;

public sealed record DocumentListResponse(IReadOnlyList<DocumentSummaryDto> Items);