namespace Cortex.API.Extensions;

using Cortex.API.Authorization;
using Cortex.API.Models;
using Cortex.API.Handlers;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this WebApplication app)
    {
        var comments = app.MapGroup("/api/tickets/{ticketId}/comments")
            .RequireAuthorization()
            .WithTags("Comments");

        comments.MapGet("/", CommentHandlers.GetComment)
            .WithName("GetAllComments")
            .Produces<List<Comment>>(StatusCodes.Status200OK);

        comments.MapPost("/", CommentHandlers.CreateComment)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("CreateComment")
            .Produces<Comment>(StatusCodes.Status201Created);
    }

    public record CreateCommentRequest(string Body, string? CreatedBy);
}
