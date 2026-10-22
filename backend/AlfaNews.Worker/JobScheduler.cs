using AlfaNews.Core.Ingest;

namespace AlfaNews.Worker;

public sealed class JobScheduler(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<JobScheduler> logger) : BackgroundService
{
    private async Task LoadSourceCatalogAsync(CancellationToken cancellationToken)
    {
        var path = configuration["Sources:CatalogPath"] ?? Path.Combine(configuration["Data:InputPath"] ?? "data", "sources.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Каталог источников не найден: {Path}.", path);
            return;
        }
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<SourceCatalogLoader>().LoadAsync(path, cancellationToken);
            logger.LogInformation("Каталог источников загружен: {Count} записей.", count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Не удалось загрузить каталог источников: {ErrorType}.", exception.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadSourceCatalogAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(120));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<JobSchedule>().EnqueueAsync(stoppingToken);
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