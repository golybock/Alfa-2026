namespace AlfaNews.Contracts.Processing;

public sealed record AnswerChatJob(string UserId, string InsightId, string Question, string ChatTurnId);