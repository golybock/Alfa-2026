namespace AlfaNews.Contracts.Api;

public sealed record ChatHistoryResponse(IReadOnlyList<ChatTurnDto> Items);