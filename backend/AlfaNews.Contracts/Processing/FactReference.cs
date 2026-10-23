namespace AlfaNews.Contracts.Processing;

public sealed record FactReference(string Text, int Line, int? Start, int? End);