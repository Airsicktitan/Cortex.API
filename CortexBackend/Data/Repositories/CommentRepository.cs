using Cortex.API.Database;
using Cortex.API.Models;

using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class CommentRepository(CortexDbContext context) : ICommentRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IEnumerable<Comment>> GetCommentsByTicketIdAsync(string ticketId)
    {
        return await _context.Comments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ArchivedComment>> GetArchivedCommentsByTicketIdAsync(string ticketId)
    {
        return await _context.ArchivedComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<Comment> CreateCommentAsync(Comment comment)
    {
        await _context.Comments.AddAsync(comment);
        return comment;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
