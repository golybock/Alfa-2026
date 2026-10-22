namespace AlfaNews.Contracts.Processing;

public sealed record AgentPlan(IReadOnlyList<ToolCall> Calls, IReadOnlyList<string> QuestionsToResolve);