namespace AlfaNews.Core.Processing;

public sealed record JobResult(string Stage = "done", string? ResultType = null, string? ResultId = null, string? ErrorCode = null);