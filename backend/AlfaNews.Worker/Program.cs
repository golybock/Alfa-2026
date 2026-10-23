using System.Globalization;
using AlfaNews.Worker;
using AlfaNews.Core.Data;
using AlfaNews.Core.Importing;
using AlfaNews.Core.Ingest;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Profiles;
using AlfaNews.Core.Processing;
using AlfaNews.Core.Configuration;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Chat;
using AlfaNews.Worker.Chat;
using AlfaNews.Worker.Documents;
using AlfaNews.Worker.Evaluation;
using AlfaNews.Worker.Feed;
using AlfaNews.Worker.Insights;
using AlfaNews.Worker.Importing;
using AlfaNews.Worker.News;
using AlfaNews.Worker.Profiles;
using AlfaNews.Worker.Queue;
using Microsoft.EntityFrameworkCore;

LocalEnvironment.Load();
var importCommand = args.FirstOrDefault() == "import-csv";
var profilesCommand = args.FirstOrDefault() == "rebuild-profiles";
var retryCommand = args.FirstOrDefault() == "retry-job";
var runCommand = args.FirstOrDefault() == "run-worker";
var evaluationCommand = args.FirstOrDefault() == "llm-evaluate";
var newsValidationCommand = args.FirstOrDefault() == "news-validate";
var demoCommand = args.FirstOrDefault() == "precompute-demo";
var manualCommand = importCommand || profilesCommand || retryCommand || evaluationCommand || newsValidationCommand || demoCommand;
var demoAsOf = DateTimeOffset.UtcNow;
if (demoCommand && (args.Length > 2 || args.Length == 2 && !DateTimeOffset.TryParse(args[1], CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out demoAsOf)))
    throw new ArgumentException("Использование: precompute-demo [as_of, например 2026-10-25T09:00:00Z].");
if (newsValidationCommand && (args.Length != 4 || !int.TryParse(args[3], out var parseLimit) || parseLimit is < 1 or > 10))
    throw new ArgumentException("Использование: news-validate <report.json> <source-id> <parse-limit:1-10>.");
if (evaluationCommand && args.Length is not (3 or 4))
    throw new ArgumentException("Использование: llm-evaluate <cases.json> <report.json> [case1,case2].");
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
var pdfOptions = new PdfOptions();
builder.Configuration.GetSection("Storage").Bind(pdfOptions);
pdfOptions.InputPath = builder.Configuration["Data:InputPath"] ?? pdfOptions.InputPath;
builder.Services.AddSingleton(pdfOptions);
builder.Services.AddScoped<PdfTools>();
builder.Services.AddScoped<PdfRegistration>();
builder.Services.AddScoped<PdfProcessing>();
builder.Services.AddScoped<PdfScan>();
var llmOptions = new LlmOptions();
builder.Configuration.GetSection("Llm").Bind(llmOptions);
builder.Services.AddAlfaNewsLlm(llmOptions);
builder.Services.AddScoped<JobQueue>();
var queueOptions = new QueueOptions();
builder.Configuration.GetSection("Queue").Bind(queueOptions);
queueOptions.Validate();
builder.Services.AddSingleton(queueOptions);
builder.Services.AddSingleton<JobDispatcher>();
builder.Services.AddScoped<IJobHandler, ImportCsvJobHandler>();
builder.Services.AddScoped<IJobHandler, RebuildProfilesJobHandler>();
builder.Services.AddScoped<IJobHandler, ConsoleMessageJobHandler>();
builder.Services.AddScoped<IJobHandler, IndexPdfJobHandler>();
builder.Services.AddScoped<IJobHandler, OcrPdfPageJobHandler>();
builder.Services.AddScoped<IJobHandler, FinalizePdfJobHandler>();
builder.Services.AddScoped<IJobHandler, EmbedContentJobHandler>();
builder.Services.AddScoped<ResearchSearch>();
builder.Services.AddScoped<IJobHandler, CollectNewsJobHandler>();
builder.Services.AddSingleton(_ => new SafeFetcher(builder.Configuration.GetValue<bool>("Ingest:AllowPrivateAddresses")));
builder.Services.AddScoped<SourceCatalogLoader>();
builder.Services.AddScoped<CollectNewsService>();
builder.Services.AddScoped<NewsProcessingSchedule>();
builder.Services.AddScoped<NewsTriageService>();
builder.Services.AddScoped<NewsProcessing>();
builder.Services.AddSingleton(provider => new NewsEmbeddings(new HttpClient { Timeout = TimeSpan.FromSeconds(90) }, provider.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<IJobHandler, ParseNewsJobHandler>();
builder.Services.AddScoped<IJobHandler, TriageNewsJobHandler>();
var rankingOptions = new RankingOptions();
builder.Configuration.GetSection("Ranking").Bind(rankingOptions);
rankingOptions.CompanyIds = (builder.Configuration["Feed:CompanyIds"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
rankingOptions.Validate();
builder.Services.AddSingleton(rankingOptions);
builder.Services.AddScoped<TaxonomyCatalogLoader>();
builder.Services.AddScoped<FeedRefreshService>();
builder.Services.AddScoped<IJobHandler, RefreshFeedJobHandler>();
builder.Services.AddScoped<IJobHandler, PrecomputeDemoJobHandler>();
builder.Services.AddScoped<NewsSearch>();
builder.Services.AddScoped<AgentTools>();
builder.Services.AddScoped<InsightAgent>();
builder.Services.AddScoped<IJobHandler, GenerateInsightJobHandler>();
builder.Services.AddScoped<ChatResponder>();
builder.Services.AddScoped<IJobHandler, AnswerChatJobHandler>();
builder.Services.AddScoped<JobSchedule>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<JobScheduler>();
builder.Services.AddHostedService<TestJobScheduler>();
builder.Services.AddHostedService<PdfScheduler>();
builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<ProfileBuilder>();
builder.Services.AddScoped<ProfileBuildService>();
builder.Services.AddScoped<LlmEvaluation>();
builder.Services.AddScoped<NewsValidation>();

using var host = builder.Build();
await using (var migrationScope = host.Services.CreateAsyncScope())
    await migrationScope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>().Database.MigrateAsync(
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
if (manualCommand)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    await using var scope = host.Services.CreateAsyncScope();
    if (newsValidationCommand)
    {
        await scope.ServiceProvider.GetRequiredService<NewsValidation>().RunAsync(args[1], args[2], int.Parse(args[3]), cancellation.Token);
        return;
    }
    if (evaluationCommand)
    {
        await scope.ServiceProvider.GetRequiredService<LlmEvaluation>().RunAsync(args[1], args[2], cancellation.Token,
            args.Length == 4 ? args[3].Split(',').ToHashSet(StringComparer.Ordinal) : null);
        return;
    }
    if (demoCommand)
    {
        if (rankingOptions.CompanyIds.Count == 0)
            throw new InvalidOperationException("Задайте Feed__CompanyIds для демо-снимка.");
        var demo = await scope.ServiceProvider.GetRequiredService<JobQueue>().EnqueueAsync(
            new PrecomputeDemoJob(rankingOptions.CompanyIds, demoAsOf), null, null, cancellation.Token);
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PrecomputeDemo")
            .LogInformation("Поставлен демо-снимок {JobId} на {AsOf}; его выполнит запущенный Worker.", demo.Id, demoAsOf);
        return;
    }
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