namespace AlfaNews.Contracts.Processing;

public sealed record ChatDraft(
    string Answer,
    IReadOnlyList<DraftClaim> Claims,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> SuggestedQuestions);