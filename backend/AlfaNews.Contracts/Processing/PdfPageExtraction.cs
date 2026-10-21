namespace AlfaNews.Contracts.Processing;

public sealed record PdfPageExtraction(IReadOnlyList<ResearchBlockDraft> Blocks);