namespace AlfaNews.Contracts.Processing;

public sealed record ExtractedFact(string Text, string Quote, int? Line = null, int? Start = null, int? End = null);