namespace AlfaNews.Contracts.Api;

public sealed record ChatTurnDto(string Id, string Question, DateTimeOffset CreatedAt, ChatAnswerDto Answer);