using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record GenerateInsightJob(string CompanyId, string EventId, Role Role, string InputVersion);