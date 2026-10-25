namespace AlfaNews.Contracts.Domain;

public sealed record RelevanceReasonDto(
    string Code,
    string Text,
    IReadOnlyList<string> ClaimIds,
    IReadOnlyList<string> EvidenceRefs,
    string? Label = null
);