namespace AlfaNews.Contracts.Api;

public sealed record ApiError(string Code, string Message, string RequestId);