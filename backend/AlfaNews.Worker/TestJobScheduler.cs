using AlfaNews.Core.Processing;

namespace AlfaNews.Worker;

public sealed class TestJobScheduler(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<TestJobScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("TestJob:Enabled"))
            return;
        var text = configuration["TestJob:Text"] ?? "Очередь Worker работает.";
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<JobQueue>()
                    .EnqueueAsync(new ConsoleMessageJob(text), null, null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Не удалось поставить тестовое задание: {ErrorType}.", exception.GetType().Name);
            }
        }
    }
}