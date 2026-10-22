using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record PendingInsightResponse(AnalysisStatus Status, string? JobId);