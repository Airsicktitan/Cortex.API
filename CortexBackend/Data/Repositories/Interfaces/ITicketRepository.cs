using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Data;
public interface ITicketRepository
{
    public Task<IReadOnlyList<Ticket>> GetAllTicketsAsync(
        DateTime? modifiedSinceUtc = null,
        int? boardId = null,
        TicketVisibilityContext? visibilityFilter = null);

    public Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetTicketsPageAsync(
        int? boardId,
        TicketVisibilityContext visibility,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<int, int>> GetActiveTicketBoardCountsAsync(
        TicketVisibilityContext visibility,
        CancellationToken cancellationToken = default);

    /// <summary>Intake queue: pending first review and items returned for more detail.</summary>
    public Task<IReadOnlyList<Ticket>> GetIntakeQueueTicketsAsync(
        TicketVisibilityContext visibility,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<ArchivedTicket>> GetArchivedTicketsAsync(
        DateTime? modifiedSinceUtc = null,
        int? boardId = null,
        TicketVisibilityContext? visibilityFilter = null);

    public Task<(IReadOnlyList<ArchivedTicket> Items, int TotalCount)> GetArchivedTicketsPageAsync(
        int? boardId,
        TicketVisibilityContext visibility,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<Ticket>> GetArchiveCandidatesAsync(IReadOnlyCollection<string> statuses, DateTime olderThanUtc);
    public Task<Ticket?> GetTicketByIdAsync (string id); 
    public Task<ArchivedTicket?> GetArchivedTicketByIdAsync(string id);
    public Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(string status); 
    public Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(string priority); 
    public Task<Ticket> CreateTicketAsync(Ticket ticket); 
    public Task<Ticket> UpdateTicketAsync(Ticket ticket); 
    public Task<string> GetNextTicketIdAsync();
    public Task<bool> ArchiveTicketAsync(string id, int archivedBy);
    public Task<bool> ReactivateArchivedTicketAsync(string id, int reactivatedBy, string restoredStatus);
    public Task<bool> DeleteTicketAsync(string id); 
    public Task<IEnumerable<Ticket>> GetTicketByUserAsync(User user); 

    Task SaveChangesAsync();
}
