using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IAuth0UserDirectorySyncService
{
    /// <summary>
    /// Imports Auth0 users into the local <see cref="Models.User"/> table: match by <c>Auth0Id</c>,
    /// link legacy rows by email, create missing rows. Does not replace Auth0; updates identity fields only when safe.
    /// </summary>
    Task<SyncUsersFromAuth0Response> SyncFromAuth0Async(CancellationToken cancellationToken = default);
}
