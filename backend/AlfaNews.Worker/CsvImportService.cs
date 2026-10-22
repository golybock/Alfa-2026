using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Importing;

namespace AlfaNews.Worker;

public sealed class CsvImportService(IConfiguration configuration, CsvImporter importer, ILogger<CsvImportService> logger)
{
    public async Task ImportOnStartupAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Data:ImportOnStartup"))
            return;

        var inputPath = configuration["Data:InputPath"] ?? "data";
        var version = configuration["Data:DatasetVersion"]
            ?? throw new InvalidOperationException("Задайте Data__DatasetVersion для импорта CSV.");
        var job = new ImportCsvJob(Path.Combine(inputPath, "df_Uid_data_hack.csv"),
            Path.Combine(inputPath, "df_Uid_payment_hack.csv"), version);
        await RunAsync(job, true, cancellationToken);
    }

    public Task ImportAsync(ImportCsvJob job, CancellationToken cancellationToken) => RunAsync(job, false, cancellationToken);

    private async Task RunAsync(ImportCsvJob job, bool onlyIfMissing, CancellationToken cancellationToken)
    {
        var reportPath = configuration["Data:ImportReportPath"] ?? "data/reports/import_report.json";
        var result = await importer.ImportAsync(job, reportPath, onlyIfMissing, cancellationToken);
        if (result.Skipped)
            logger.LogInformation("CSV-пакет {DatasetVersion} уже импортирован, повторная загрузка пропущена.", job.DatasetVersion);
        else
            logger.LogInformation("CSV-пакет {DatasetVersion} импортирован: компаний {Companies}, платёжных пар {Payments}. Отчёт: {ReportPath}",
                job.DatasetVersion, result.Report!.Companies.Accepted, result.Report.Payments.Accepted, reportPath);
    }
}