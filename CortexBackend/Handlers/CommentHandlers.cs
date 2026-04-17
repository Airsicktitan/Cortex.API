namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using Cortex.API.Services;
using Cortex.API.DTO;

public static class CommentHandlers
{
    public static async Task<IResult> GetComment(
        string ticketId,
        ITicketRepository ticketRepo,
        ITicketVisibilityService ticketVisibilityService,
        ICommentRepository repo,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await ticketRepo.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var results = (await repo.GetCommentsByTicketIdAsync(ticketId)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            results.Select(comment => comment.CreatedBy));
        return Results.Ok(results.Select(comment => comment.ToResponse(mappingContext)));
    }

    public static async Task<IResult> GetArchivedComment(
        string ticketId,
        ITicketRepository ticketRepo,
        ITicketVisibilityService ticketVisibilityService,
        ICommentRepository repo,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var archivedTicket = await ticketRepo.GetArchivedTicketByIdAsync(ticketId);
        if (archivedTicket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(
                archivedTicket.CreatedBy,
                archivedTicket.SynitiOwner,
                archivedTicket.BusinessOwner))
        {
            return Results.NotFound();
        }

        var results = (await repo.GetArchivedCommentsByTicketIdAsync(ticketId)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            results.Select(comment => comment.CreatedBy));
        return Results.Ok(results.Select(comment => comment.ToResponse(mappingContext)));
    }

    public static async Task<IResult> CreateComment(
        string ticketId,
        CreateCommentRequest request,
        ICommentRepository commentRepo,
        ITicketRepository ticketRepo,
        ITicketVisibilityService ticketVisibilityService,
        IUserContextService userContext,
        ITicketAuditService ticketAuditService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await ticketRepo.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var trimmedBody = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            return Results.BadRequest(new { message = "Comment body is required." });
        }

        var currentUser = await userContext.GetCurrentUserAsync();

        var comment = await commentRepo.CreateCommentAsync(new Comment
        {
            TicketId = ticketId,
            Body = trimmedBody,
            CreatedBy = currentUser.Id,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        });

        await commentRepo.SaveChangesAsync();
        await ticketAuditService.RecordCommentAddedAsync(comment, currentUser);
        var mappingContext = await mappingContextFactory.CreateAsync([comment.CreatedBy]);
        var commentResponse = comment.ToResponse(mappingContext);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(ticket);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "comment.created",
            TicketId = ticketId,
            EntityId = comment.Id.ToString(),
            AudienceUserIds = audienceUserIds,
            Comment = commentResponse
        });

        return Results.Created(
            $"/api/tickets/{ticketId}/comments/{comment.Id}",
            commentResponse);
    }

    public static async Task<IResult> SignalTyping(
        string ticketId,
        ITicketRepository ticketRepo,
        ITicketVisibilityService ticketVisibilityService,
        IUserContextService userContext,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver)
    {
        var ticket = await ticketRepo.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(ticket);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "comment.typing",
            TicketId = ticketId,
            ActorUserId = currentUser.Id,
            ActorDisplayName = currentUser.DisplayName,
            AudienceUserIds = audienceUserIds
        });

        return Results.Accepted();
    }

    public record CreateCommentRequest(string Body);
}
