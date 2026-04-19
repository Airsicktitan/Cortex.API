using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;

using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;
public class TicketRepository(CortexDbContext context) : ITicketRepository
{ 
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<Ticket>> GetAllTicketsAsync(
        DateTime? modifiedSinceUtc = null,
        int? boardId = null,
        TicketVisibilityContext? visibilityFilter = null)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.BoardDefinition)
            .WhereApprovedForActiveWork()
            .AsQueryable();

        if (boardId.HasValue)
        {
            query = query.Where(ticket => ticket.BoardId == boardId.Value);
        }

        if (modifiedSinceUtc.HasValue)
        {
            var sinceUtc = DateTime.SpecifyKind(modifiedSinceUtc.Value, DateTimeKind.Utc);
            query = query.Where(ticket =>
                ticket.CreatedDate >= sinceUtc ||
                (ticket.LastModifiedDate.HasValue && ticket.LastModifiedDate.Value >= sinceUtc));
        }

        if (visibilityFilter is not null)
        {
            query = query.WhereVisibleTo(visibilityFilter);
        }

        return await query
            .OrderByDescending(ticket => ticket.CreatedDate)
            .ThenByDescending(ticket => ticket.Id)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetTicketsPageAsync(
        int? boardId,
        TicketVisibilityContext visibility,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default)
    {
        var filtered = _context.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.BoardDefinition)
            .WhereApprovedForActiveWork()
            .WhereVisibleTo(visibility)
            .AsQueryable();

        if (boardId.HasValue)
        {
            filtered = filtered.Where(ticket => ticket.BoardId == boardId.Value);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByTicketListSort(sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetActiveTicketBoardCountsAsync(
        TicketVisibilityContext visibility,
        CancellationToken cancellationToken = default)
    {
        var counts = await _context.Tickets
            .AsNoTracking()
            .WhereApprovedForActiveWork()
            .WhereVisibleTo(visibility)
            .GroupBy(ticket => ticket.BoardId)
            .Select(group => new
            {
                BoardId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.BoardId, entry => entry.Count);
    }

    public async Task<IReadOnlyList<Ticket>> GetIntakeQueueTicketsAsync(
        TicketVisibilityContext visibility,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.BoardDefinition)
            .Include(ticket => ticket.CreatedByUser)
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.PendingApproval)
            .WhereVisibleTo(visibility)
            .OrderByDescending(ticket => ticket.CreatedDate)
            .ThenByDescending(ticket => ticket.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivedTicket>> GetArchivedTicketsAsync(
        DateTime? modifiedSinceUtc = null,
        int? boardId = null,
        TicketVisibilityContext? visibilityFilter = null)
    {
        var query = _context.ArchivedTickets
            .AsNoTracking()
            .Include(ticket => ticket.BoardDefinition)
            .AsQueryable();

        if (boardId.HasValue)
        {
            query = query.Where(ticket => ticket.BoardId == boardId.Value);
        }

        if (modifiedSinceUtc.HasValue)
        {
            var sinceUtc = DateTime.SpecifyKind(modifiedSinceUtc.Value, DateTimeKind.Utc);
            query = query.Where(ticket =>
                ticket.ArchivedDate >= sinceUtc ||
                (ticket.LastModifiedDate.HasValue && ticket.LastModifiedDate.Value >= sinceUtc));
        }

        if (visibilityFilter is not null)
        {
            query = query.WhereVisibleTo(visibilityFilter);
        }

        return await query
            .OrderByDescending(ticket => ticket.ArchivedDate)
            .ThenByDescending(ticket => ticket.Id)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<ArchivedTicket> Items, int TotalCount)> GetArchivedTicketsPageAsync(
        int? boardId,
        TicketVisibilityContext visibility,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filtered = _context.ArchivedTickets
            .AsNoTracking()
            .Include(ticket => ticket.BoardDefinition)
            .WhereVisibleTo(visibility)
            .AsQueryable();

        if (boardId.HasValue)
        {
            filtered = filtered.Where(ticket => ticket.BoardId == boardId.Value);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(ticket => ticket.ArchivedDate)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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
            .WhereApprovedForActiveWork()
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

    public async Task<IEnumerable<Ticket>> GetTicketByUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var normalizedAuth0Id = NormalizeForMatch(user.Auth0Id);
        var normalizedEmail = NormalizeForMatch(user.Email);
        var normalizedDisplayName = NormalizeForMatch(user.DisplayName);
        var userIdToken = NormalizeForMatch(
            $"{OwnerFieldResolution.UserIdTokenPrefix}{user.Id.ToString(CultureInfo.InvariantCulture)}")
            ?? string.Empty;

        return await _context.Tickets
            .AsNoTracking()
            .Include(t => t.BoardDefinition)
            .Include(t => t.CreatedByUser)
            .Where(t =>
                t.CreatedBy == user.Id ||
                (normalizedAuth0Id != null &&
                 t.CreatedByUser != null &&
                 t.CreatedByUser.Auth0Id != null &&
                 t.CreatedByUser.Auth0Id.Trim().ToLower() == normalizedAuth0Id) ||
                (normalizedEmail != null &&
                 t.CreatedByUser != null &&
                 t.CreatedByUser.Email != null &&
                 t.CreatedByUser.Email.Trim().ToLower() == normalizedEmail) ||
                (normalizedDisplayName != null &&
                 t.SynitiOwner != null &&
                 t.SynitiOwner.Trim().ToLower() == normalizedDisplayName) ||
                (normalizedEmail != null &&
                 t.SynitiOwner != null &&
                 t.SynitiOwner.Trim().ToLower() == normalizedEmail) ||
                (t.SynitiOwner != null &&
                 t.SynitiOwner.Trim().ToLower() == userIdToken) ||
                (normalizedDisplayName != null &&
                 t.BusinessOwner != null &&
                 t.BusinessOwner.Trim().ToLower() == normalizedDisplayName) ||
                (normalizedEmail != null &&
                 t.BusinessOwner != null &&
                 t.BusinessOwner.Trim().ToLower() == normalizedEmail) ||
                (t.BusinessOwner != null &&
                 t.BusinessOwner.Trim().ToLower() == userIdToken))
            .OrderByDescending(t => t.CreatedDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(string status)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Include(t => t.BoardDefinition)
            .WhereApprovedForActiveWork()
            .Where(t => t.Status == status)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(string priority)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Include(t => t.BoardDefinition)
            .WhereApprovedForActiveWork()
            .Where(t => t.Priority == priority)
            .ToListAsync();
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
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR dbo.TicketIdSequence";

            var result = await command.ExecuteScalarAsync();
            if (result is null || result is DBNull)
            {
                throw new InvalidOperationException("Failed to get next ticket sequence value.");
            }

            var nextId = Convert.ToInt64(result, CultureInfo.InvariantCulture);
            return nextId.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
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
            ApprovalStatus = ApprovalStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = reactivatedBy,
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

    private static string? NormalizeForMatch(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
