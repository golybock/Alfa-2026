using AlfaNews.Worker;
using AlfaNews.Core.Data;
using AlfaNews.Core.Importing;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Profiles;
using AlfaNews.Core.Processing;

var importCommand = args.FirstOrDefault() == "import-csv";
var profilesCommand = args.FirstOrDefault() == "rebuild-profiles";
var retryCommand = args.FirstOrDefault() == "retry-job";
var runCommand = args.FirstOrDefault() == "run-worker";
var manualCommand = importCommand || profilesCommand || retryCommand;
if (retryCommand && (args.Length != 2 || !Guid.TryParse(args[1], out _)))
    throw new ArgumentException("Использование: retry-job <id>.");
if (importCommand && args.Length != 4)
    throw new ArgumentException("Использование: import-csv <company.csv> <payment.csv> <dataset_version>.");
if (profilesCommand && args.Length != 2)
    throw new ArgumentException("Использование: rebuild-profiles <dataset_version>.");
var builder = Host.CreateApplicationBuilder(manualCommand ? [] : runCommand ? args[1..] : args);
builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<JobQueue>();
builder.Services.AddSingleton<JobDispatcher>();
builder.Services.AddScoped<IJobHandler, ImportCsvJobHandler>();
builder.Services.AddScoped<IJobHandler, RebuildProfilesJobHandler>();
builder.Services.AddScoped<IJobHandler, ConsoleMessageJobHandler>();
builder.Services.AddScoped<JobSchedule>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<JobScheduler>();
builder.Services.AddHostedService<TestJobScheduler>();
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
    if (retryCommand)
    {
        var retried = await scope.ServiceProvider.GetRequiredService<JobQueue>().RetryAsync(Guid.Parse(args[1]), cancellation.Token);
        if (!retried)
            throw new InvalidOperationException("Задание не найдено либо не находится в состоянии failed/running.");
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RetryJob")
            .LogInformation("Задание {JobId} возвращено в очередь.", args[1]);
        return;
    }
    if (importCommand)
        await scope.ServiceProvider.GetRequiredService<CsvImportService>().ImportAsync(
            new ImportCsvJob(args[1], args[2], args[3]), cancellation.Token);
    await scope.ServiceProvider.GetRequiredService<ProfileBuildService>().BuildAsync(
        new RebuildProfilesJob(importCommand ? args[3] : args[1]), cancellation.Token);
    return;
}
await host.RunAsync();