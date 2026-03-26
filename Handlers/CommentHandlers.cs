namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Database;

using Microsoft.EntityFrameworkCore;
using Cortex.API.Data;
using Cortex.API.Services;

public static class CommentHandlers
{
    public static async Task<IResult> GetComment(string ticketId, ICommentRepository repo)
    {
        var results = await repo.GetCommentsByTicketIdAsync(ticketId);

            return Results.Ok(results);
    }

    public static async Task<IResult> CreateComment(string ticketId, CreateCommentRequest request, ICommentRepository commentRepo, ITicketRepository ticketRepo, IUserContextService userContext)
    {
        var ticket = await ticketRepo.GetTicketByIdAsync(ticketId);
        if (ticket is null)
            return Results.NotFound();

        var currentUser = await userContext.GetCurrentUserAsync();
            
        var comment = await commentRepo.CreateCommentAsync(new Comment
            {
                TicketId = ticketId,
                Body = request.Body,
                CreatedBy = currentUser.Id,
                CreatedDate = DateTime.UtcNow
            });

            await commentRepo.SaveChangesAsync();

            return Results.Created(
                $"/api/tickets/{ticketId}/comments/{comment.Id}",
                comment
            );
    }
    public record CreateCommentRequest(string Body);
}
