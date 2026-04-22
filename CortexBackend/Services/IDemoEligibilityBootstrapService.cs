namespace Cortex.API.Services;

public interface IDemoEligibilityBootstrapService
{
    Task<int> EnsureDemoEligibilityAsync(CancellationToken cancellationToken = default);
}
