using Cortex.API.Models;

namespace Cortex.API.Services;

public class NoOpUserAccessSyncService(ILogger<NoOpUserAccessSyncService> logger) : IUserAccessSyncService
{
    private readonly ILogger<NoOpUserAccessSyncService> _logger = logger;

    public Task QueueUserAccessSyncAsync(
        User user,
        IReadOnlyList<string> requestedPermissions,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Queued no-op access sync for user {UserId}. Role={Role}. RequestedPermissions={Permissions}",
            user.Id,
            user.Role,
            string.Join(',', requestedPermissions));

        return Task.CompletedTask;
    }
}
