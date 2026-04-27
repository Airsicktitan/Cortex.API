namespace Cortex.API.Extensions;

using Cortex.API.Authorization;
using Cortex.API.DTO;
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
            .Produces<List<CommentResponse>>(StatusCodes.Status200OK);

        comments.MapPost("/", CommentHandlers.CreateComment)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("CreateComment")
            .Produces<CommentResponse>(StatusCodes.Status201Created);

        comments.MapPost("/typing", CommentHandlers.SignalTyping)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("SignalCommentTyping")
            .Produces(StatusCodes.Status202Accepted);

        var archivedComments = app.MapGroup("/api/tickets/archived/{ticketId}/comments")
            .RequireAuthorization()
            .WithTags("Comments");

        archivedComments.MapGet("/", CommentHandlers.GetArchivedComment)
            .WithName("GetAllArchivedComments")
            .Produces<List<CommentResponse>>(StatusCodes.Status200OK);
    }

    public record CreateCommentRequest(string Body, string? CreatedBy);
}
