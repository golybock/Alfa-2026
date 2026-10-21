namespace AlfaNews.Contracts.Api;

public sealed record JobResponse(
    string Id, JobStatus Status, string Stage, string? ResultType,
    string? ResultId, ApiError? Error);