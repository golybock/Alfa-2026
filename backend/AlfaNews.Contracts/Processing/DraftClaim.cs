using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record DraftClaim(string Id, string Text, Origin Origin, IReadOnlyList<string> Refs, Confidence? Confidence);