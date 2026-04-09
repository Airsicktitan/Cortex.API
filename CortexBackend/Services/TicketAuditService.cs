using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class TicketAuditService(CortexDbContext context) : ITicketAuditService
{
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<TicketAuditEntry>> GetTicketHistoryAsync(string ticketId)
    {
        return await _context.TicketAuditEntries
            .Include(entry => entry.ChangedByUser)
            .Include(entry => entry.FieldChanges)
            .Where(entry => entry.TicketId == ticketId)
            .OrderByDescending(entry => entry.ChangedDateUtc)
            .ThenByDescending(entry => entry.Id)
            .ToListAsync();
    }

    public async Task RecordTicketCreatedAsync(Ticket ticket, User changedByUser, string? reason)
    {
        var fieldChanges = new List<TicketAuditFieldChange>();
        AddFieldChange(fieldChanges, "Title", null, ticket.Title);
        AddFieldChange(fieldChanges, "Description", null, ticket.Description);
        AddFieldChange(fieldChanges, "Status", null, ticket.Status);
        AddFieldChange(fieldChanges, "Priority", null, ticket.Priority);
        AddFieldChange(fieldChanges, "Syniti Owner", null, ticket.SynitiOwner);
        AddFieldChange(fieldChanges, "Business Owner", null, ticket.BusinessOwner);

        await AddEntryAsync(
            ticket.Id,
            "Created",
            "Ticket created",
            string.IsNullOrWhiteSpace(reason) ? "Ticket created." : reason,
            changedByUser,
            fieldChanges);
    }

    public async Task RecordTicketUpdatedAsync(
        Ticket originalTicket,
        Ticket updatedTicket,
        User changedByUser,
        string? reason)
    {
        var fieldChanges = new List<TicketAuditFieldChange>();
        AddFieldChange(fieldChanges, "Title", originalTicket.Title, updatedTicket.Title);
        AddFieldChange(fieldChanges, "Description", originalTicket.Description, updatedTicket.Description);
        AddFieldChange(fieldChanges, "Status", originalTicket.Status, updatedTicket.Status);
        AddFieldChange(fieldChanges, "Priority", originalTicket.Priority, updatedTicket.Priority);
        AddFieldChange(fieldChanges, "Syniti Owner", originalTicket.SynitiOwner, updatedTicket.SynitiOwner);
        AddFieldChange(fieldChanges, "Business Owner", originalTicket.BusinessOwner, updatedTicket.BusinessOwner);

        if (fieldChanges.Count == 0)
        {
            return;
        }

        await AddEntryAsync(
            updatedTicket.Id,
            "Updated",
            fieldChanges.Count == 1 ? "Updated 1 field" : $"Updated {fieldChanges.Count} fields",
            NormalizeOptionalValue(reason),
            changedByUser,
            fieldChanges);
    }

    public async Task RecordTicketArchivedAsync(Ticket ticket, User changedByUser, string? reason)
    {
        var fieldChanges = new List<TicketAuditFieldChange>();
        AddFieldChange(fieldChanges, "Lifecycle", "Active", "Archived");

        await AddEntryAsync(
            ticket.Id,
            "Archived",
            "Ticket archived",
            string.IsNullOrWhiteSpace(reason) ? "Ticket moved out of the active queue." : reason,
            changedByUser,
            fieldChanges);
    }

    public async Task RecordTicketReactivatedAsync(
        ArchivedTicket archivedTicket,
        Ticket restoredTicket,
        User changedByUser,
        string? reason)
    {
        var fieldChanges = new List<TicketAuditFieldChange>();
        AddFieldChange(fieldChanges, "Lifecycle", "Archived", "Active");
        AddFieldChange(fieldChanges, "Status", archivedTicket.Status, restoredTicket.Status);

        await AddEntryAsync(
            restoredTicket.Id,
            "Reactivated",
            "Ticket reactivated",
            string.IsNullOrWhiteSpace(reason)
                ? "Ticket restored from archive."
                : reason,
            changedByUser,
            fieldChanges);
    }

    public async Task RecordCommentAddedAsync(Comment comment, User changedByUser)
    {
        await AddEntryAsync(
            comment.TicketId,
            "CommentAdded",
            "Comment added",
            null,
            changedByUser,
            [new TicketAuditFieldChange
            {
                FieldName = "Comment",
                NewValue = comment.Body
            }]);
    }

    public async Task RecordAttachmentsAddedAsync(
        string ticketId,
        IEnumerable<TicketAttachment> attachments,
        User changedByUser)
    {
        var uploadedAttachments = attachments.ToList();
        if (uploadedAttachments.Count == 0)
        {
            return;
        }

        var fieldChanges = uploadedAttachments
            .Select(attachment => new TicketAuditFieldChange
            {
                FieldName = "Attachment",
                NewValue = attachment.FileName
            })
            .ToList();

        await AddEntryAsync(
            ticketId,
            "AttachmentAdded",
            uploadedAttachments.Count == 1
                ? "Uploaded 1 attachment"
                : $"Uploaded {uploadedAttachments.Count} attachments",
            null,
            changedByUser,
            fieldChanges);
    }

    private async Task AddEntryAsync(
        string ticketId,
        string action,
        string summary,
        string? reason,
        User changedByUser,
        List<TicketAuditFieldChange> fieldChanges)
    {
        var auditEntry = new TicketAuditEntry
        {
            TicketId = ticketId,
            Action = action,
            Summary = summary,
            Reason = NormalizeOptionalValue(reason),
            ChangedBy = changedByUser.Id,
            ChangedDateUtc = DateTime.UtcNow,
            FieldChanges = fieldChanges
        };

        await _context.TicketAuditEntries.AddAsync(auditEntry);
        await _context.SaveChangesAsync();
    }

    private static void AddFieldChange(
        ICollection<TicketAuditFieldChange> fieldChanges,
        string fieldName,
        string? oldValue,
        string? newValue)
    {
        var normalizedOldValue = NormalizeOptionalValue(oldValue);
        var normalizedNewValue = NormalizeOptionalValue(newValue);

        if (string.Equals(normalizedOldValue, normalizedNewValue, StringComparison.Ordinal))
        {
            return;
        }

        fieldChanges.Add(new TicketAuditFieldChange
        {
            FieldName = fieldName,
            OldValue = normalizedOldValue,
            NewValue = normalizedNewValue
        });
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
