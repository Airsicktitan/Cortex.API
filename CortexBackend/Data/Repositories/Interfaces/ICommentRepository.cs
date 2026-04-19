using Cortex.API.Models;

namespace Cortex.API.Data;

public interface ICommentRepository
{
    Task<int> CountCommentsByTicketIdAsync(string ticketId);

    public Task<IEnumerable<Comment>> GetCommentsByTicketIdAsync(string ticketId);
    public Task<IEnumerable<ArchivedComment>> GetArchivedCommentsByTicketIdAsync(string ticketId);
    public Task<Comment> CreateCommentAsync(Comment comment);
    Task SaveChangesAsync();
}