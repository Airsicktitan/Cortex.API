namespace Cortex.API.Services;

public class SlaNotificationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaNotificationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<SlaNotificationHostedService> _logger = logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        await ProcessNotificationsOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessNotificationsOnceAsync(stoppingToken);
        }
    }

    private async Task ProcessNotificationsOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var createdCount = await notificationService.ProcessSlaNotificationsAsync(DateTime.UtcNow, cancellationToken);

            if (createdCount > 0)
            {
                _logger.LogInformation(
                    "Created {NotificationCount} SLA notification(s).",
                    createdCount);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed while processing SLA notifications.");
        }
        finally
        {
            _runLock.Release();
        }
    }
}
