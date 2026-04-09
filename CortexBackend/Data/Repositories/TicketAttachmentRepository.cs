using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class TicketAttachmentRepository(CortexDbContext context)
    : ITicketAttachmentRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IEnumerable<TicketAttachment>> GetByTicketIdAsync(string ticketId)
    {
        return await _context.TicketAttachments
            .Where(attachment => attachment.TicketId == ticketId)
            .OrderByDescending(attachment => attachment.UploadedDate)
            .ThenByDescending(attachment => attachment.Id)
            .ToListAsync();
    }

    public async Task<TicketAttachment?> GetByIdAsync(int id)
    {
        return await _context.TicketAttachments
            .FirstOrDefaultAsync(attachment => attachment.Id == id);
    }

    public async Task AddRangeAsync(IEnumerable<TicketAttachment> attachments)
    {
        await _context.TicketAttachments.AddRangeAsync(attachments);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
