namespace AlfaNews.Contracts.Api;

public sealed record DocumentSummaryDto(
    string Id, string Title, DateOnly? PublishedOn, int PageCount, string Version,
    OcrStatus OcrStatus, ReviewStatus ReviewStatus);