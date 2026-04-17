using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IRoleDefinitionRepository
{
    Task<List<RoleDefinition>> GetAllAsync();
    Task<RoleDefinition?> GetByIdAsync(int id);
    Task<RoleDefinition?> GetByNameAsync(string name);
    Task AddAsync(RoleDefinition definition);
    void Delete(RoleDefinition definition);
    Task SaveChangesAsync();
}
