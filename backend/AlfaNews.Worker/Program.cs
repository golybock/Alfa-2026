using AlfaNews.Worker;
using AlfaNews.Core.Data;
using AlfaNews.Core.Importing;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Profiles;

var importCommand = args.FirstOrDefault() == "import-csv";
var profilesCommand = args.FirstOrDefault() == "rebuild-profiles";
var manualCommand = importCommand || profilesCommand;
if (importCommand && args.Length != 4)
    throw new ArgumentException("Использование: import-csv <company.csv> <payment.csv> <dataset_version>.");
if (profilesCommand && args.Length != 2)
    throw new ArgumentException("Использование: rebuild-profiles <dataset_version>.");
var builder = Host.CreateApplicationBuilder(manualCommand ? [] : args);
builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<ProfileBuilder>();
builder.Services.AddScoped<ProfileBuildService>();

using var host = builder.Build();
if (manualCommand)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    await using var scope = host.Services.CreateAsyncScope();
    if (importCommand)
        await scope.ServiceProvider.GetRequiredService<CsvImportService>().ImportAsync(
            new ImportCsvJob(args[1], args[2], args[3]), cancellation.Token);
    await scope.ServiceProvider.GetRequiredService<ProfileBuildService>().BuildAsync(
        new RebuildProfilesJob(importCommand ? args[3] : args[1]), cancellation.Token);
    return;
}
await host.RunAsync();