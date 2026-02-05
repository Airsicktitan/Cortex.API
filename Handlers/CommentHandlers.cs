namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Database;

using Microsoft.EntityFrameworkCore;

public static class CommentHandlers
{
    public static async Task<IResult> GetAllComments(CortexDbContext db)
    {
        var comments = await db.Comments.ToListAsync();
        return Results.Ok(comments);
    }

    public static async Task<IResult> CreateComment(string ticketId, CreateCommentRequest request, CortexDbContext db)
    {
        var ticketExists = await db.Tickets.AnyAsync(t => t.Id == ticketId);
            if (!ticketExists) return Results.NotFound();

            var comment = new Comment
            {
                TicketId = ticketId,
                Body = request.Body,
                CreatedBy = request.CreatedBy ?? "System",
                CreatedDate = DateTime.UtcNow
            };

            db.Comments.Add(comment);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/tickets/{ticketId}/comments/{comment.Id}",
                comment
            );
    }
    public record CreateCommentRequest(string Body, string? CreatedBy);
}
