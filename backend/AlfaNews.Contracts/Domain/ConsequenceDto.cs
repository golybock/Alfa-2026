namespace AlfaNews.Contracts.Domain;

public sealed record ConsequenceDto(ConsequenceType Type, IReadOnlyList<string> ClaimIds);