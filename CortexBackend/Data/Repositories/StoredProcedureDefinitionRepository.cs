using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class StoredProcedureDefinitionRepository(CortexDbContext context) : IStoredProcedureDefinitionRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<List<StoredProcedureDefinition>> GetAllAsync()
    {
        return await _context.StoredProcedureDefinitions
            .OrderBy(definition => definition.Name)
            .ToListAsync();
    }

    public Task<StoredProcedureDefinition?> GetByIdAsync(int id)
    {
        return _context.StoredProcedureDefinitions.FirstOrDefaultAsync(definition => definition.Id == id);
    }

    public Task<StoredProcedureDefinition?> GetByNameAsync(string name)
    {
        return _context.StoredProcedureDefinitions.FirstOrDefaultAsync(definition => definition.Name == name);
    }

    public Task<StoredProcedureDefinition?> GetByProcedureNameAsync(string procedureName)
    {
        return _context.StoredProcedureDefinitions.FirstOrDefaultAsync(definition => definition.ProcedureName == procedureName);
    }

    public async Task AddAsync(StoredProcedureDefinition definition)
    {
        await _context.StoredProcedureDefinitions.AddAsync(definition);
    }

    public void Delete(StoredProcedureDefinition definition)
    {
        _context.StoredProcedureDefinitions.Remove(definition);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
