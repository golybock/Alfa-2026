using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using AlfaNews.Worker.Importing;
using AlfaNews.Worker.Profiles;

namespace AlfaNews.Worker.Queue;

public class Worker(IServiceScopeFactory scopes, JobDispatcher dispatcher, QueueOptions queue, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            var recovered = await scope.ServiceProvider.GetRequiredService<JobQueue>().RecoverAsync(stoppingToken);
            logger.LogInformation("Возвращено в очередь незавершённых заданий: {Count}.", recovered);
            var abandoned = await scope.ServiceProvider.GetRequiredService<LlmBudget>().ReleaseAbandonedAsync(stoppingToken);
            logger.LogInformation("Закрыто брошенных запросов к модели: {Count}.", abandoned);
        }

        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CsvImportService>().ImportOnStartupAsync(stoppingToken);

        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ProfileBuildService>().BuildOnStartupAsync(stoppingToken);

        var slots = queue.LaneSlots();
        logger.LogInformation("Исполнители полос: {Slots}.", string.Join(", ", slots.Select(lane => lane.Key + "=" + lane.Value)));
        await Task.WhenAll(slots.SelectMany(lane => Enumerable.Range(0, lane.Value)
            .Select(_ => Task.Run(() => RunLaneAsync(lane.Key, JobKinds.InLane(lane.Key), stoppingToken), stoppingToken))));
    }

    private async Task RunLaneAsync(string lane, string[] kinds, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await dispatcher.DispatchNextAsync(kinds, stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Ошибка доступа к очереди {Lane}: {ErrorType}.", lane, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}