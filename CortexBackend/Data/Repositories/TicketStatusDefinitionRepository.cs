using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class TicketStatusDefinitionRepository(CortexDbContext context) : ITicketStatusDefinitionRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<List<TicketStatusDefinition>> GetAllAsync()
    {
        return await _context.TicketStatusDefinitions
            .OrderBy(definition => definition.Id)
            .ToListAsync();
    }

    public Task<TicketStatusDefinition?> GetByIdAsync(int id)
    {
        return _context.TicketStatusDefinitions.FirstOrDefaultAsync(definition => definition.Id == id);
    }

    public Task<TicketStatusDefinition?> GetByNameAsync(string name)
    {
        return _context.TicketStatusDefinitions.FirstOrDefaultAsync(definition => definition.Name == name);
    }

    public async Task AddAsync(TicketStatusDefinition definition)
    {
        await _context.TicketStatusDefinitions.AddAsync(definition);
    }

    public void Delete(TicketStatusDefinition definition)
    {
        _context.TicketStatusDefinitions.Remove(definition);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
