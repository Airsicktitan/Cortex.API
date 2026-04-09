namespace Cortex.API.Services;

public class ScheduledJobHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledJobHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ScheduledJobHostedService> _logger = logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        await RunDueJobsOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunDueJobsOnceAsync(stoppingToken);
        }
    }

    private async Task RunDueJobsOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scheduledJobService = scope.ServiceProvider.GetRequiredService<IScheduledJobService>();
            var executedCount = await scheduledJobService.RunDueJobsAsync(DateTime.UtcNow);

            if (executedCount > 0)
            {
                _logger.LogInformation("Executed {ExecutedCount} scheduled job(s).", executedCount);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed while executing scheduled jobs.");
        }
        finally
        {
            _runLock.Release();
        }
    }
}
