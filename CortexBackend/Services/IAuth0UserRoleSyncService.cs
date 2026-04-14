using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Syncs the Cortex database role to Auth0 user app_metadata / user_metadata (no API permissions).
/// </summary>
public interface IAuth0UserRoleSyncService
{
    Task SyncRoleToAuth0Async(User user, CancellationToken cancellationToken = default);
}
