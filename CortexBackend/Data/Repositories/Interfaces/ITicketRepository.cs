using Cortex.API.Models;

namespace Cortex.API.Data;
public interface ITicketRepository
{ 
    public Task<IEnumerable<Ticket>> GetAllTicketsAsync(); 
    public Task<IEnumerable<ArchivedTicket>> GetArchivedTicketsAsync();
    public Task<IReadOnlyList<Ticket>> GetArchiveCandidatesAsync(IReadOnlyCollection<string> statuses, DateTime olderThanUtc);
    public Task<Ticket?> GetTicketByIdAsync (string id); 
    public Task<ArchivedTicket?> GetArchivedTicketByIdAsync(string id);
    public Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(string status); 
    public Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(string priority); 
    public Task<Ticket> CreateTicketAsync(Ticket ticket); 
    public Task<Ticket> UpdateTicketAsync(Ticket ticket); 
    public Task<bool> ArchiveTicketAsync(string id, int archivedBy);
    public Task<bool> ReactivateArchivedTicketAsync(string id, int reactivatedBy, string restoredStatus);
    public Task<bool> DeleteTicketAsync(int id); 
    public Task<IEnumerable<Ticket>> GetTicketByUserAsync (int id); 

    Task SaveChangesAsync();
}
