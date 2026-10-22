using AlfaNews.Worker;
using AlfaNews.Core.Data;
using AlfaNews.Core.Importing;
using AlfaNews.Contracts.Processing;

var importCommand = args.FirstOrDefault() == "import-csv";
if (importCommand && args.Length != 4)
    throw new ArgumentException("Использование: import-csv <company.csv> <payment.csv> <dataset_version>.");
var builder = Host.CreateApplicationBuilder(importCommand ? [] : args);
builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<CsvImportService>();

using var host = builder.Build();
if (importCommand)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CsvImportService>().ImportAsync(
        new ImportCsvJob(args[1], args[2], args[3]), cancellation.Token);
    return;
}
await host.RunAsync();