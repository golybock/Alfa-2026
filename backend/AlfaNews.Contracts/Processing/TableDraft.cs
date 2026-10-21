namespace AlfaNews.Contracts.Processing;

public sealed record TableDraft(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);