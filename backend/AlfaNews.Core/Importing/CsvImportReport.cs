namespace AlfaNews.Core.Importing;

public sealed record CsvImportReport(
    string DatasetVersion,
    string CompanySha256,
    string PaymentSha256,
    CsvTableReport Companies,
    CsvTableReport Payments);

public sealed record CsvTableReport(
    long Read,
    long Accepted,
    long AcceptedSourceRows,
    long MergedRows,
    IReadOnlyDictionary<string, long> Excluded,
    IReadOnlyDictionary<string, long> Quality);

public sealed record CsvImportResult(bool Skipped, CsvImportReport? Report);