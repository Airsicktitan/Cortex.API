using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IStoredProcedureDefinitionService
{
    Task<IReadOnlyList<StoredProcedureDefinition>> GetAllAsync();
    Task<StoredProcedureDefinition> CreateAsync(StoredProcedureDefinition definition);
    Task<StoredProcedureDefinition> UpdateAsync(int id, StoredProcedureDefinition definition);
    Task DeleteAsync(int id);
}
