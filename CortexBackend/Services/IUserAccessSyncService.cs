using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IUserAccessSyncService
{
    Task QueueUserAccessSyncAsync(
        User user,
        IReadOnlyList<string> requestedPermissions,
        CancellationToken cancellationToken = default);
}
