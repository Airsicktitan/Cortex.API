using System.Security.Claims;
using Cortex.API.Database;
using Cortex.API.Models;

using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;
public class TicketRepository(CortexDbContext context) : ITicketRepository
{ 
    private readonly CortexDbContext _context = context;

    public async Task<IEnumerable<Ticket>> GetAllTicketsAsync()
    {
        return await _context.Tickets.Include(t => t.CreatedByUser).ToListAsync();
    }

    public async Task<IEnumerable<ArchivedTicket>> GetArchivedTicketsAsync()
    {
        return await _context.ArchivedTickets
            .Include(ticket => ticket.CreatedByUser)
            .Include(ticket => ticket.ArchivedByUser)
            .OrderByDescending(ticket => ticket.ArchivedDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetArchiveCandidatesAsync(
        IReadOnlyCollection<string> statuses,
        DateTime olderThanUtc)
    {
        if (statuses.Count == 0)
        {
            return [];
        }

        return await _context.Tickets
            .Include(ticket => ticket.CreatedByUser)
            .Where(ticket =>
                statuses.Contains(ticket.Status) &&
                (ticket.LastModifiedDate ?? ticket.CreatedDate) <= olderThanUtc)
            .OrderBy(ticket => ticket.LastModifiedDate ?? ticket.CreatedDate)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicketByIdAsync(string id)
    {
        return await _context.Tickets.Include(t => t.CreatedByUser).FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ArchivedTicket?> GetArchivedTicketByIdAsync(string id)
    {
        return await _context.ArchivedTickets
            .Include(ticket => ticket.CreatedByUser)
            .Include(ticket => ticket.ArchivedByUser)
            .FirstOrDefaultAsync(ticket => ticket.Id == id);
    }

    public async Task<IEnumerable<Ticket>> GetTicketByUserAsync(int user)
    {
        return await _context.Tickets.Include(t => t.CreatedByUser).Where(t => t.CreatedBy == user).ToListAsync();
    }


    public async Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(string status)
    {
        return await _context.Tickets.Where(t => t.Status == status).ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(string priority)
    {
        return await _context.Tickets.Where(t => t.Priority == priority).ToListAsync();
    }

    public async Task<Ticket> CreateTicketAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        return await _context.Tickets
            .Include(t => t.CreatedByUser)
            .FirstAsync(t => t.Id == ticket.Id);
    }

    public async Task<Ticket> UpdateTicketAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return await Task.FromResult(ticket);
    }

    public async Task<bool> ArchiveTicketAsync(string id, int archivedBy)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.ArchiveTicket @TicketId={id}, @ArchivedBy={archivedBy}");

        return await _context.ArchivedTickets.AnyAsync(ticket => ticket.Id == id);
    }

    public async Task<bool> DeleteTicketAsync(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return false;
        }
        _context.Tickets.Remove(ticket);
        return true;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
