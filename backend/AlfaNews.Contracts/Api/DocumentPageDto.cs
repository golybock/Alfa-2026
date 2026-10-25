using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record DocumentPageDto(
    int PageNo,
    OcrStatus OcrStatus,
    ReviewStatus ReviewStatus,
    string PageImageUrl,
    IReadOnlyList<ReviewableChunkDto> Chunks,
    string? Error
);