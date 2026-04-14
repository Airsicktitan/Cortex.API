using Cortex.API.Models;

namespace Cortex.API.Services;

public class NoOpAuth0UserRoleSyncService(ILogger<NoOpAuth0UserRoleSyncService> logger) : IAuth0UserRoleSyncService
{
    private readonly ILogger<NoOpAuth0UserRoleSyncService> _logger = logger;

    public Task SyncRoleToAuth0Async(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("No-op Auth0 role sync for user {UserId}, role {Role}.", user.Id, user.Role);
        return Task.CompletedTask;
    }
}
