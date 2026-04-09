using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ITicketAuditService
{
    Task<IReadOnlyList<TicketAuditEntry>> GetTicketHistoryAsync(string ticketId);
    Task RecordTicketCreatedAsync(Ticket ticket, User changedByUser, string? reason);
    Task RecordTicketUpdatedAsync(Ticket originalTicket, Ticket updatedTicket, User changedByUser, string? reason);
    Task RecordTicketArchivedAsync(Ticket ticket, User changedByUser, string? reason);
    Task RecordTicketReactivatedAsync(ArchivedTicket archivedTicket, Ticket restoredTicket, User changedByUser, string? reason);
    Task RecordCommentAddedAsync(Comment comment, User changedByUser);
    Task RecordAttachmentsAddedAsync(string ticketId, IEnumerable<TicketAttachment> attachments, User changedByUser);
}
