namespace AlfaNews.Contracts.Processing;

public sealed record PrecomputeDemoJob(IReadOnlyList<string> CompanyIds, DateTimeOffset AsOf);