namespace AlfaNews.Contracts.Processing;

public sealed record ChatDraft(IReadOnlyList<DraftClaim> Claims, IReadOnlyList<string> SuggestedQuestions);