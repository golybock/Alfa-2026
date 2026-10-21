namespace AlfaNews.Contracts.Processing;

public sealed record OcrPdfPageJob(string DocumentId, int PageNo);