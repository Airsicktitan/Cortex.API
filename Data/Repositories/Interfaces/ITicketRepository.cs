using Cortex.API.Models;

namespace Cortex.API.Data;
public interface ITicketRepository
{ 
    public Task<IEnumerable<Ticket>> GetAllTicketsAsync(); 
    public Task<Ticket?> GetTicketByIdAsync (string id); 
    public Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(string status); 
    public Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(string priority); 
    public Task<Ticket> CreateTicketAsync(Ticket ticket); 
    public Task<Ticket> UpdateTicketAsync(Ticket ticket); 
    public Task<bool> DeleteTicketAsync(int id); 
    public Task<IEnumerable<Ticket>> GetTicketByUserAsync (string id); 

    Task SaveChangesAsync();
}