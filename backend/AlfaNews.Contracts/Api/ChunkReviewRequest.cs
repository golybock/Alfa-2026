using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Contracts.Api;

public sealed record ChunkReviewRequest(
    string ChunkId,
    string CorrectedText,
    ReviewStatus ReviewStatus,
    IReadOnlyList<TableDraft> CorrectedTables,
    IReadOnlyList<ChartReviewRequest> Charts,
    string? Note
);