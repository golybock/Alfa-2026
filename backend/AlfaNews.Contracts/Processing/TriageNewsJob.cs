namespace AlfaNews.Contracts.Processing;

public sealed record TriageNewsJob(IReadOnlyList<string> NewsRawIds);