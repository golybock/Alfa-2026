using AlfaNews.Core.Processing;
using AlfaNews.Worker.Importing;
using AlfaNews.Worker.Profiles;

namespace AlfaNews.Worker.Queue;

public class Worker(IServiceScopeFactory scopes, JobDispatcher dispatcher, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            var recovered = await scope.ServiceProvider.GetRequiredService<JobQueue>().RecoverAsync(stoppingToken);
            logger.LogInformation("Возвращено в очередь незавершённых заданий: {Count}.", recovered);
        }

        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CsvImportService>().ImportOnStartupAsync(stoppingToken);

        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ProfileBuildService>().BuildOnStartupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await dispatcher.DispatchNextAsync(stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Ошибка доступа к очереди: {ErrorType}.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}