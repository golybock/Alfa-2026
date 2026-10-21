namespace AlfaNews.Contracts.Processing;

public sealed record CollectNewsJob(IReadOnlyList<string> SourceIds);