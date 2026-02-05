namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

using Microsoft.EntityFrameworkCore;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this WebApplication app)
    {
        var comments = app.MapGroup("/api/tickets/{ticketId}/comments")
            .WithTags("Comments");

        comments.MapGet("/", async (string ticketId, CortexDbContext db) =>
        {
            var results = await db.Comments
                .Where(c => c.TicketId == ticketId)
                .OrderBy(c => c.CreatedDate)
                .ToListAsync();

            return Results.Ok(results);
        });

        comments.MapPost("/", async (
            string ticketId,
            CreateCommentRequest request,
            CortexDbContext db
        ) =>
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
        });
    }
    public record CreateCommentRequest(string Body, string? CreatedBy);
}
