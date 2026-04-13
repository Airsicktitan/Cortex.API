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

    public Task<TicketBoardDefinition?> GetByNameAsync(string name)
    {
        return _context.TicketBoardDefinitions.FirstOrDefaultAsync(definition =>
            definition.Name == name);
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

    public async Task NormalizeBoardAssignmentsAsync(int defaultBoardId)
    {
        var validBoardIds = await _context.TicketBoardDefinitions
            .Select(definition => definition.Id)
            .ToListAsync();

        var invalidTickets = await _context.Tickets
            .Where(ticket => ticket.BoardId <= 0 || !validBoardIds.Contains(ticket.BoardId))
            .ToListAsync();
        foreach (var ticket in invalidTickets)
        {
            ticket.BoardId = defaultBoardId;
        }

        var invalidArchivedTickets = await _context.ArchivedTickets
            .Where(ticket => ticket.BoardId <= 0 || !validBoardIds.Contains(ticket.BoardId))
            .ToListAsync();
        foreach (var archivedTicket in invalidArchivedTickets)
        {
            archivedTicket.BoardId = defaultBoardId;
        }

        if (invalidTickets.Count > 0 || invalidArchivedTickets.Count > 0)
        {
            await _context.SaveChangesAsync();
        }
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
