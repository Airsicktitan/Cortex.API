using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IRoleDefinitionService
{
    IReadOnlyCollection<string> AllowedPermissions { get; }
    Task<IReadOnlyList<RoleDefinition>> GetAllAsync();
    Task<RoleDefinition> CreateAsync(RoleDefinition definition);
    Task<RoleDefinition> UpdateAsync(int id, RoleDefinition definition);
    Task DeleteAsync(int id);
}
