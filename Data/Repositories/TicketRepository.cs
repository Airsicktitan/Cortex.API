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

    public async Task<Ticket?> GetTicketByIdAsync(string id)
    {
        return await _context.Tickets.Include(t => t.CreatedByUser).FirstOrDefaultAsync(t => t.Id == id);
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
        return ticket;
    }

    public async Task<Ticket> UpdateTicketAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return await Task.FromResult(ticket);
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