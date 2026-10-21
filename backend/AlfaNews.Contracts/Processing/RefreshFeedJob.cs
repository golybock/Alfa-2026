namespace AlfaNews.Contracts.Processing;

public sealed record RefreshFeedJob(string CompanyId, DateTimeOffset AsOf);