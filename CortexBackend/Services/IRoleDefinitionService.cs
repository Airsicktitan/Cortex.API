using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IRoleDefinitionService
{
    IReadOnlyCollection<string> AllowedPermissions { get; }
    Task<IReadOnlyList<RoleDefinition>> GetAllAsync();
    Task<RoleDefinition> CreateAsync(RoleDefinition definition);
    Task<RoleDefinition> UpdateAsync(int id, RoleDefinition definition);
    Task DeleteAsync(int id);

    /// <summary>
    /// Ensures each Auth0 tenant role has a Cortex <see cref="RoleDefinition"/> row (matched by name, case-insensitive).
    /// Existing rows are left unchanged.
    /// </summary>
    Task<SyncRoleDefinitionsFromAuth0Response> SyncFromAuth0Async(CancellationToken cancellationToken = default);
}
