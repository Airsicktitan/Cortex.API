using Cortex.API.Models;

namespace Cortex.API.Data;

public interface ITicketAttachmentRepository
{
    Task<IEnumerable<TicketAttachment>> GetByTicketIdAsync(string ticketId);
    Task<IEnumerable<ArchivedTicketAttachment>> GetArchivedByTicketIdAsync(string ticketId);
    Task<TicketAttachment?> GetByIdAsync(int id);
    Task<ArchivedTicketAttachment?> GetArchivedByIdAsync(int id);
    Task AddRangeAsync(IEnumerable<TicketAttachment> attachments);
    Task SaveChangesAsync();
}
