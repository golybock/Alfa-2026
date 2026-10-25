using AlfaNews.Core.Feed;
using AlfaNews.Core.Ingest;

namespace AlfaNews.Worker.Queue;

public sealed class JobScheduler(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<JobScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private async Task LoadSourceCatalogAsync(CancellationToken cancellationToken)
    {
        var inputPath = configuration["Data:InputPath"] ?? "data";
        var catalogIds = new List<string>();
        var complete = true;
        foreach (var file in new[] { "sources.json", "sources-specialized.json" })
        {
            var path = file == "sources.json" ? configuration["Sources:CatalogPath"] ?? Path.Combine(inputPath, file) : Path.Combine(inputPath, file);
            if (!File.Exists(path))
            {
                logger.LogWarning("Каталог источников не найден: {Path}.", path);
                complete = false;
                continue;
            }
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ids = await scope.ServiceProvider.GetRequiredService<SourceCatalogLoader>().LoadAsync(path, cancellationToken, file == "sources-specialized.json");
                catalogIds.AddRange(ids);
                logger.LogInformation("Каталог источников {File} загружен: {Count} записей.", file, ids.Count);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("Не удалось загрузить каталог источников {File}: {ErrorType}.", file, exception.GetType().Name);
                complete = false;
            }
        }
        if (!complete)
            return;
        await using var disableScope = scopes.CreateAsyncScope();
        var disabled = await disableScope.ServiceProvider.GetRequiredService<SourceCatalogLoader>().DisableAbsentAsync(catalogIds, cancellationToken);
        if (disabled > 0)
            logger.LogInformation("Выключено источников вне каталогов: {Count}.", disabled);
    }

    private async Task LoadTaxonomyAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(configuration["Data:InputPath"] ?? "data", "taxonomy.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Каталог таксономии не найден: {Path}.", path);
            return;
        }
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<TaxonomyCatalogLoader>().LoadAsync(path, cancellationToken);
            logger.LogInformation("Каталог таксономии загружен: {Count} факторов.", count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Не удалось загрузить каталог таксономии: {ErrorType}.", exception.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadSourceCatalogAsync(stoppingToken);
        await LoadTaxonomyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var schedule = scope.ServiceProvider.GetRequiredService<JobSchedule>();
                await schedule.EnqueueCollectionAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<NewsProcessingSchedule>().EnqueueAsync(stoppingToken);
                await schedule.EnqueueFeedRefreshAsync(stoppingToken);
                await schedule.EnqueueMissingEmbeddingsAsync(stoppingToken);
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