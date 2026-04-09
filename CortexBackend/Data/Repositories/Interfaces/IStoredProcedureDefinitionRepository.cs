using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IStoredProcedureDefinitionRepository
{
    Task<List<StoredProcedureDefinition>> GetAllAsync();
    Task<StoredProcedureDefinition?> GetByIdAsync(int id);
    Task<StoredProcedureDefinition?> GetByNameAsync(string name);
    Task<StoredProcedureDefinition?> GetByProcedureNameAsync(string procedureName);
    Task AddAsync(StoredProcedureDefinition definition);
    void Delete(StoredProcedureDefinition definition);
    Task SaveChangesAsync();
}
