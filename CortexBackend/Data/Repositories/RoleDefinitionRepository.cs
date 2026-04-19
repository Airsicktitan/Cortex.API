using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class RoleDefinitionRepository(CortexDbContext context) : IRoleDefinitionRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<List<RoleDefinition>> GetAllAsync()
    {
        return await _context.RoleDefinitions
            .OrderBy(definition => definition.Name)
            .ThenBy(definition => definition.Id)
            .ToListAsync();
    }

    public Task<RoleDefinition?> GetByIdAsync(int id)
    {
        return _context.RoleDefinitions.FirstOrDefaultAsync(definition => definition.Id == id);
    }

    public Task<RoleDefinition?> GetByNameAsync(string name)
    {
        return _context.RoleDefinitions.FirstOrDefaultAsync(definition => definition.Name == name);
    }

    public Task<RoleDefinition?> GetByNameIgnoreCaseAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<RoleDefinition?>(null);
        }

        var trimmed = name.Trim();
        var key = trimmed.ToUpperInvariant();
        return _context.RoleDefinitions.FirstOrDefaultAsync(definition =>
            definition.NameNormalized == key);
    }

    public async Task AddAsync(RoleDefinition definition)
    {
        await _context.RoleDefinitions.AddAsync(definition);
    }

    public void Delete(RoleDefinition definition)
    {
        _context.RoleDefinitions.Remove(definition);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
