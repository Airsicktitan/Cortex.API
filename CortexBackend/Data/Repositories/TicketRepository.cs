using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.Models;

using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;
public class TicketRepository(CortexDbContext context) : ITicketRepository
{ 
    private readonly CortexDbContext _context = context;

    public async Task<IEnumerable<Ticket>> GetAllTicketsAsync()
    {
        return await _context.Tickets
        .Include(t => t.BoardDefinition)
        .ToListAsync();
    }

    public async Task<IEnumerable<ArchivedTicket>> GetArchivedTicketsAsync()
    {
        return await _context.ArchivedTickets
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
            .Where(ticket =>
                statuses.Contains(ticket.Status) &&
                (ticket.LastModifiedDate ?? ticket.CreatedDate) <= olderThanUtc)
            .OrderBy(ticket => ticket.LastModifiedDate ?? ticket.CreatedDate)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicketByIdAsync(string id)
    {
        return await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ArchivedTicket?> GetArchivedTicketByIdAsync(string id)
    {
        return await _context.ArchivedTickets
            .FirstOrDefaultAsync(ticket => ticket.Id == id);
    }

    public async Task<IEnumerable<Ticket>> GetTicketByUserAsync(int user)
    {
        return await _context.Tickets.Where(t => t.CreatedBy == user).ToListAsync();
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
        return ticket;
    }

    public async Task<Ticket> UpdateTicketAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return await Task.FromResult(ticket);
    }

    public async Task<string> GetNextTicketIdAsync()
    {
        // NEXT VALUE FOR is atomic at the database level — safe under concurrent inserts.
        // The sequence is seeded above the current max by migration AddTicketIdSequence.
        var nextId = await _context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR dbo.TicketIdSequence AS [Value]")
            .SingleAsync();

        return nextId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<bool> ArchiveTicketAsync(string id, int archivedBy)
    {
        await EfSqlGuardrails.ExecuteArchiveTicketAsync(_context.Database, id, archivedBy);

        return await _context.ArchivedTickets.AnyAsync(ticket => ticket.Id == id);
    }

    public async Task<bool> ReactivateArchivedTicketAsync(string id, int reactivatedBy, string restoredStatus)
    {
        var archivedTicket = await _context.ArchivedTickets
            .FirstOrDefaultAsync(ticket => ticket.Id == id);

        if (archivedTicket is null)
        {
            return false;
        }

        if (await _context.Tickets.AnyAsync(ticket => ticket.Id == id))
        {
            return false;
        }

        var archivedComments = await _context.ArchivedComments
            .Where(comment => comment.TicketId == id)
            .OrderBy(comment => comment.CreatedDate)
            .ThenBy(comment => comment.Id)
            .ToListAsync();

        var archivedAttachments = await _context.ArchivedTicketAttachments
            .Where(attachment => attachment.TicketId == id)
            .OrderBy(attachment => attachment.UploadedDate)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync();

        var legacyPlaceholderAttachments = archivedAttachments
            .Where(IsLegacyPlaceholderAttachment)
            .ToList();
        var restorableArchivedAttachments = archivedAttachments
            .Where(attachment => !IsLegacyPlaceholderAttachment(attachment))
            .ToList();

        var restoredTicket = new Ticket
        {
            Id = archivedTicket.Id,
            Title = archivedTicket.Title,
            Description = archivedTicket.Description,
            Status = restoredStatus,
            Priority = archivedTicket.Priority,
            BoardId = archivedTicket.BoardId,
            StoryPoints = archivedTicket.StoryPoints,
            SynitiOwner = archivedTicket.SynitiOwner,
            BusinessOwner = archivedTicket.BusinessOwner,
            CreatedBy = archivedTicket.CreatedBy,
            CreatedDate = archivedTicket.CreatedDate,
            LastModifiedBy = reactivatedBy,
            LastModifiedDate = DateTime.UtcNow
        };

        var restoredComments = archivedComments.Select(comment => new Comment
        {
            TicketId = id,
            Body = comment.Body,
            CreatedBy = comment.CreatedBy,
            CreatedDate = comment.CreatedDate,
            LastModifiedDate = comment.LastModifiedDate
        }).ToList();

        var legacyAttachmentRestoreNote = BuildLegacyAttachmentRestoreNote(legacyPlaceholderAttachments);
        if (!string.IsNullOrWhiteSpace(legacyAttachmentRestoreNote))
        {
            restoredComments.Add(new Comment
            {
                TicketId = id,
                Body = legacyAttachmentRestoreNote,
                CreatedBy = reactivatedBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            });
        }

        var restoredAttachments = restorableArchivedAttachments.Select(attachment => new TicketAttachment
        {
            TicketId = id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            Content = attachment.Content,
            UploadedBy = attachment.UploadedBy,
            UploadedDate = attachment.UploadedDate
        });

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.Tickets.AddAsync(restoredTicket);
        await _context.Comments.AddRangeAsync(restoredComments);
        await _context.TicketAttachments.AddRangeAsync(restoredAttachments);
        _context.ArchivedComments.RemoveRange(archivedComments);
        _context.ArchivedTicketAttachments.RemoveRange(archivedAttachments);
        _context.ArchivedTickets.Remove(archivedTicket);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> DeleteTicketAsync(string id)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == id);
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

    private static bool IsLegacyPlaceholderAttachment(ArchivedTicketAttachment attachment)
    {
        if (attachment.OriginalAttachmentId.HasValue ||
            !string.Equals(attachment.ContentType, "text/plain", StringComparison.OrdinalIgnoreCase) ||
            attachment.Content.Length == 0)
        {
            return false;
        }

        var contentText = Encoding.UTF8.GetString(attachment.Content);
        return contentText.Contains(
            "binary attachment preservation was enabled",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildLegacyAttachmentRestoreNote(
        IReadOnlyList<ArchivedTicketAttachment> placeholderAttachments)
    {
        if (placeholderAttachments.Count == 0)
        {
            return null;
        }

        var fileLabels = placeholderAttachments
            .Select(GetLegacyAttachmentLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var listedFiles = string.Join(", ", fileLabels);
        var additionalCount = placeholderAttachments.Count - fileLabels.Count;
        var moreSuffix = additionalCount > 0
            ? $" and {additionalCount} more"
            : string.Empty;

        return
            $"System restore note: {placeholderAttachments.Count} legacy archived attachment(s) could not be restored as files because the original binary content was not preserved when this ticket was first archived. Missing attachment(s): {listedFiles}{moreSuffix}.";
    }

    private static string GetLegacyAttachmentLabel(ArchivedTicketAttachment attachment)
    {
        const string LegacySuffix = ".legacy.txt";

        if (attachment.FileName.EndsWith(LegacySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return attachment.FileName[..^LegacySuffix.Length];
        }

        return attachment.FileName;
    }
}
