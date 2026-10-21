namespace AlfaNews.Contracts.Api;

public sealed record ReviewAcceptedResponse(string DocumentId, string Version, string JobId);