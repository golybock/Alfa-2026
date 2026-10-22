namespace AlfaNews.Contracts.Processing;

public sealed record SearchResearchArguments(string Query, IReadOnlyList<string> Factors, int Limit);