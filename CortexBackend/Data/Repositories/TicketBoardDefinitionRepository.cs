using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class TicketBoardDefinitionRepository(CortexDbContext context) : ITicketBoardDefinitionRepository
{
    private readonly CortexDbContext _context = context;

    public Task<List<TicketBoardDefinition>> GetAllAsync()
    {
        return _context.TicketBoardDefinitions
            .OrderBy(definition => definition.Name)
            .ThenBy(definition => definition.Id)
            .ToListAsync();
    }

    public Task<TicketBoardDefinition?> GetByIdAsync(int id)
    {
        return _context.TicketBoardDefinitions.FirstOrDefaultAsync(definition => definition.Id == id);
    }

    public Task AddAsync(TicketBoardDefinition definition)
    {
        return _context.TicketBoardDefinitions.AddAsync(definition).AsTask();
    }

    public async Task<bool> IsBoardInUseAsync(int id)
    {
        return await _context.Tickets.AnyAsync(ticket => ticket.BoardId == id)
            || await _context.ArchivedTickets.AnyAsync(ticket => ticket.BoardId == id);
    }

    public void Delete(TicketBoardDefinition definition)
    {
        _context.TicketBoardDefinitions.Remove(definition);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
