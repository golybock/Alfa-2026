namespace AlfaNews.Contracts.Processing;

public sealed record ImportCsvJob(string CompanyFile, string PaymentFile, string DatasetVersion);