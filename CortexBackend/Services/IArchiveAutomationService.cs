namespace Cortex.API.Services;

public interface IArchiveAutomationService
{
    Task EnsurePolicySchedulerAsync(int runAsUserId);
}
