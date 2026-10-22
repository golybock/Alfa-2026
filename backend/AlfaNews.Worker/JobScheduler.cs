namespace AlfaNews.Worker;

public sealed class JobScheduler(IServiceScopeFactory scopes, ILogger<JobScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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