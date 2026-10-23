using AlfaNews.Core.Ingest;

namespace AlfaNews.Worker.Queue;

public sealed class JobScheduler(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<JobScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private const int FeedRefreshEveryTicks = 4;

    private async Task LoadSourceCatalogAsync(CancellationToken cancellationToken)
    {
        var inputPath = configuration["Data:InputPath"] ?? "data";
        foreach (var file in new[] { "sources.json", "sources-specialized.json" })
        {
            var path = file == "sources.json" ? configuration["Sources:CatalogPath"] ?? Path.Combine(inputPath, file) : Path.Combine(inputPath, file);
            if (!File.Exists(path))
            {
                logger.LogWarning("Каталог источников не найден: {Path}.", path);
                continue;
            }
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<SourceCatalogLoader>().LoadAsync(path, cancellationToken, file == "sources-specialized.json");
                logger.LogInformation("Каталог источников {File} загружен: {Count} записей.", file, count);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("Не удалось загрузить каталог источников {File}: {ErrorType}.", file, exception.GetType().Name);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadSourceCatalogAsync(stoppingToken);
        using var timer = new PeriodicTimer(TickInterval);
        var tick = 0;
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var schedule = scope.ServiceProvider.GetRequiredService<JobSchedule>();
                await schedule.EnqueueCollectionAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<NewsProcessingSchedule>().EnqueueAsync(stoppingToken);
                if (tick++ % FeedRefreshEveryTicks == 0)
                    await schedule.EnqueueFeedRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Не удалось поставить задания по расписанию: {ErrorType}.", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}