using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record DocumentAcceptedResponse(string DocumentId, string JobId, ReviewStatus ReviewStatus);