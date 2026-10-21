namespace AlfaNews.Contracts.Api;

public sealed record PageReviewRequest(IReadOnlyList<ChunkReviewRequest> Chunks);