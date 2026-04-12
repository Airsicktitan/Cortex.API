namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Services;

/// <summary>
/// Defines all ticket-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via repository pattern.
///
/// Known Limitations:
/// - POST endpoint still uses sequential ticket ID generation
/// - Sequential ID generation is vulnerable to concurrency collisions under heavy parallel creates
/// </summary>
public static class TicketHandlers
{
    public static async Task<IResult> GetAllTickets(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var tickets = await repo.GetAllTicketsAsync();
        var visibleTickets = tickets.Where(visibilityContext.CanView).ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets
                .Where(ticket => ticket.BoardId.HasValue)
                .Select(ticket => ticket.BoardId.Value));

        return Results.Ok(
            visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> GetTicketById(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [ticket.CreatedBy],
            null,
            ticket.BoardId.HasValue ? [ticket.BoardId.Value] : null);

        return Results.Ok(ticket.ToResponse(slaConfigurations, mappingContext));
    }

    public static async Task<IResult> GetTicketHistory(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
            if (archivedTicket is null)
            {
                return Results.NotFound();
            }

            var archivedVisibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
            if (!archivedVisibilityContext.CanView(
                    archivedTicket.CreatedBy,
                    archivedTicket.SynitiOwner,
                    archivedTicket.BusinessOwner))
            {
                return Results.NotFound();
            }
        }
        else
        {
            var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
            if (!visibilityContext.CanView(ticket))
            {
                return Results.NotFound();
            }
        }

        var history = (await ticketAuditService.GetTicketHistoryAsync(id)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            history.Select(entry => entry.ChangedBy));
        return Results.Ok(history.Select(entry => entry.ToResponse(mappingContext)));
    }

    public static async Task<IResult> GetArchivedTickets(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var archivedTickets = await repo.GetArchivedTicketsAsync();
        var visibleTickets = archivedTickets.Where(ticket =>
            visibilityContext.CanView(ticket.CreatedBy, ticket.SynitiOwner, ticket.BusinessOwner)).ToList();

        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }),
            null,
            visibleTickets.Select(ticket => ticket.BoardId));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(mappingContext)));
    }

    public static async Task<IResult> GetTicketsByStatus(
        string status,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByStatusAsync(status);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets
                .Where(ticket => ticket.BoardId.HasValue)
                .Select(ticket => ticket.BoardId.Value));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> GetTicketsByPriority(
        string priority,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByPriorityAsync(priority);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets
                .Where(ticket => ticket.BoardId.HasValue)
                .Select(ticket => ticket.BoardId.Value));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> GetTicketsByUser(
        IUserContextService userContext,
        ITicketRepository repo,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var tickets = (await repo.GetTicketByUserAsync(currentUser.Id)).ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        var mappingContext = await mappingContextFactory.CreateAsync(
            tickets.Select(ticket => ticket.CreatedBy),
            null,
            tickets
                .Where(ticket => ticket.BoardId.HasValue)
                .Select(ticket => ticket.BoardId.Value));

        return Results.Ok(tickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> CreateTicket(
        CreateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        try
        {
            var nextTicketId = await repo.GetNextTicketIdAsync();
            var currentUser = await userContext.GetCurrentUserAsync();
            var requestedStatus = string.IsNullOrWhiteSpace(request.Status)
                ? await ticketStatusService.GetDefaultCreateStatusAsync()
                : request.Status.Trim();
            var routingDepartment = NormalizeOptionalValue(request.Department)
                ?? NormalizeOptionalValue(currentUser.Department);
            var routingResolution = await ticketRoutingRuleService.ResolveOwnersAsync(
                routingDepartment,
                request.Title);
            var resolvedSynitiOwner = NormalizeOptionalValue(request.SynitiOwner)
                ?? routingResolution.SynitiOwner;
            var resolvedBusinessOwner = NormalizeOptionalValue(request.BusinessOwner)
                ?? routingResolution.BusinessOwner
                ?? GetDefaultBusinessOwner(currentUser);

            await ticketStatusService.EnsureSelectableStatusAsync(requestedStatus);

            var selectedBoard = request.BoardId.HasValue
                ? await GetSelectableBoardAsync(ticketBoardService, request.BoardId.Value)
                : await ticketBoardService.GetDefaultCreateBoardAsync();
            var storyPoints = ResolveStoryPoints(
                selectedBoard,
                request.StoryPoints,
                null);

            var ticket = new Ticket
            {
                Id = nextTicketId,
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                Priority = request.Priority ?? "Medium",
                BoardId = selectedBoard.Id,
                StoryPoints = storyPoints,
                SynitiOwner = resolvedSynitiOwner,
                BusinessOwner = resolvedBusinessOwner,
                Status = requestedStatus,
                CreatedBy = currentUser.Id,
                CreatedDate = DateTime.UtcNow
            };

            await repo.CreateTicketAsync(ticket);
            await repo.SaveChangesAsync();

            var createdTicket = await repo.GetTicketByIdAsync(ticket.Id);

            if (createdTicket is null)
                return Results.Problem("Ticket was created but could not be retrieved.");

            await ticketAuditService.RecordTicketCreatedAsync(
                createdTicket,
                currentUser,
                request.ChangeReason);
            await notificationService.CreateAssignmentNotificationsForNewTicketAsync(
                createdTicket,
                currentUser);
            await realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "ticket.created",
                TicketId = createdTicket.Id,
                EntityId = createdTicket.Id
            });

            var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
            var mappingContext = await mappingContextFactory.CreateAsync(
                [createdTicket.CreatedBy],
                null,
                createdTicket.BoardId.HasValue ? [createdTicket.BoardId.Value] : null);

            return Results.Created(
                $"/api/tickets/{createdTicket.Id}",
                createdTicket.ToResponse(slaConfigurations, mappingContext));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateTicket(
        string id,
        UpdateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        try
        {
            var existing = await repo.GetTicketByIdAsync(id);
            var currentUser = await userContext.GetCurrentUserAsync();

            if (existing is null)
                return Results.NotFound();

            var originalTicket = CloneTicket(existing);

            if (!string.IsNullOrWhiteSpace(request.Status)
                && !string.Equals(request.Status, existing.Status, StringComparison.OrdinalIgnoreCase))
            {
                await ticketStatusService.EnsureSelectableStatusAsync(request.Status);
            }

            if (!existing.BoardId.HasValue)
            {
                return Results.BadRequest(new { message = "The current ticket does not have a board assigned." });
            }

            var targetBoard = request.BoardId.HasValue
                ? await GetBoardForUpdateAsync(ticketBoardService, request.BoardId.Value, existing.BoardId.Value)
                : await GetExistingBoardAsync(ticketBoardService, existing.BoardId.Value);

            var storyPoints = ResolveStoryPoints(
                targetBoard,
                request.StoryPoints,
                existing.StoryPoints);

            existing.Title = request.Title ?? existing.Title;
            existing.Description = request.Description ?? existing.Description;
            existing.Status = request.Status ?? existing.Status;
            existing.Priority = request.Priority ?? existing.Priority;
            existing.BoardId = targetBoard.Id;
            existing.StoryPoints = storyPoints;
            existing.SynitiOwner = request.SynitiOwner ?? existing.SynitiOwner;
            existing.BusinessOwner = request.BusinessOwner ?? existing.BusinessOwner;
            existing.LastModifiedBy = currentUser.Id;
            existing.LastModifiedDate = DateTime.UtcNow;

            await repo.UpdateTicketAsync(existing);
            await repo.SaveChangesAsync();

            var updatedTicket = await repo.GetTicketByIdAsync(id);

            if (updatedTicket is null)
                return Results.Problem("Ticket was updated but could not be retrieved.");

            await ticketAuditService.RecordTicketUpdatedAsync(
                originalTicket,
                updatedTicket,
                currentUser,
                request.ChangeReason);
            await notificationService.CreateAssignmentNotificationsAsync(
                originalTicket,
                updatedTicket,
                currentUser);
            await realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "ticket.updated",
                TicketId = updatedTicket.Id,
                EntityId = updatedTicket.Id
            });

            var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
            var mappingContext = await mappingContextFactory.CreateAsync(
                [updatedTicket.CreatedBy],
                null,
                updatedTicket.BoardId.HasValue ? [updatedTicket.BoardId.Value] : null);

            return Results.Ok(updatedTicket.ToResponse(slaConfigurations, mappingContext));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> ArchiveTicket(
        string id,
        TicketActionReasonRequest? request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var existing = await repo.GetTicketByIdAsync(id);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(existing))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var archived = await repo.ArchiveTicketAsync(id, currentUser.Id);
        if (!archived)
        {
            return Results.Problem("Ticket could not be archived.");
        }

        await ticketAuditService.RecordTicketArchivedAsync(
            existing,
            currentUser,
            request?.ChangeReason);
        await notificationService.CreateArchiveNotificationsAsync(
            existing,
            currentUser,
            ticketIsArchived: true);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.archived",
            TicketId = existing.Id,
            EntityId = existing.Id
        });

        var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
        if (archivedTicket is null)
        {
            return Results.Problem("Ticket was archived but could not be retrieved.");
        }

        var mappingContext = await mappingContextFactory.CreateAsync(
            [archivedTicket.CreatedBy, archivedTicket.ArchivedBy],
            null,
            [archivedTicket.BoardId]);

        return Results.Ok(archivedTicket.ToResponse(mappingContext));
    }

    public static async Task<IResult> ReactivateArchivedTicket(
        string id,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
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

        var activeTicket = await repo.GetTicketByIdAsync(id);
        if (activeTicket is not null)
        {
            return Results.Conflict(new { message = "An active ticket with this ID already exists." });
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var restoredStatus = await ticketStatusService.GetReactivatedStatusAsync(archivedTicket.Status);
        var reactivated = await repo.ReactivateArchivedTicketAsync(id, currentUser.Id, restoredStatus);
        if (!reactivated)
        {
            return Results.Problem("Ticket could not be reactivated.");
        }

        var restoredTicket = await repo.GetTicketByIdAsync(id);
        if (restoredTicket is null)
        {
            return Results.Problem("Ticket was reactivated but could not be retrieved.");
        }

        await ticketAuditService.RecordTicketReactivatedAsync(
            archivedTicket,
            restoredTicket,
            currentUser,
            null);
        await notificationService.CreateArchiveNotificationsAsync(
            restoredTicket,
            currentUser,
            ticketIsArchived: false,
            isReactivated: true);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.reactivated",
            TicketId = restoredTicket.Id,
            EntityId = restoredTicket.Id
        });

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [restoredTicket.CreatedBy],
            null,
            restoredTicket.BoardId.HasValue ? [restoredTicket.BoardId.Value] : null);

        return Results.Ok(restoredTicket.ToResponse(slaConfigurations, mappingContext));
    }

    public static async Task<IResult> DeleteTicket(string id, ITicketRepository repo)
    {
        var normalizedId = id.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return Results.BadRequest("Invalid ticket id.");
        }

        var deleted = await repo.DeleteTicketAsync(normalizedId);

        if (!deleted)
            return Results.NotFound();

        await repo.SaveChangesAsync();

        return Results.NoContent();
    }

    private static Ticket CloneTicket(Ticket ticket)
    {
        return new Ticket
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? GetDefaultBusinessOwner(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        return NormalizeOptionalValue(user.Email);
    }

    private static int? ResolveStoryPoints(
        TicketBoardDefinition board,
        int? requestedStoryPoints,
        int? existingStoryPoints)
    {
        if (!board.RequiresStoryPoints)
        {
            return null;
        }

        var resolvedStoryPoints = requestedStoryPoints ?? existingStoryPoints;
        if (!resolvedStoryPoints.HasValue)
        {
            throw new ArgumentException(
                $"Board \"{board.Name}\" requires story points from 1 to 5.");
        }

        if (resolvedStoryPoints.Value is < 1 or > 5)
        {
            throw new ArgumentException("Story points must be between 1 and 5.");
        }

        return resolvedStoryPoints.Value;
    }

    private static async Task<TicketBoardDefinition> GetSelectableBoardAsync(
        ITicketBoardService ticketBoardService,
        int boardId)
    {
        var board = await ticketBoardService.GetByIdAsync(boardId)
            ?? throw new KeyNotFoundException("Ticket board was not found.");

        if (!board.IsEnabled)
        {
            throw new ArgumentException(
                $"Board \"{board.Name}\" is disabled and cannot be assigned to new tickets.");
        }

        return board;
    }

    private static async Task<TicketBoardDefinition> GetBoardForUpdateAsync(
        ITicketBoardService ticketBoardService,
        int boardId,
        int existingBoardId)
    {
        var board = await ticketBoardService.GetByIdAsync(boardId)
            ?? throw new KeyNotFoundException("Ticket board was not found.");

        if (!board.IsEnabled && boardId != existingBoardId)
        {
            throw new ArgumentException(
                $"Board \"{board.Name}\" is disabled and cannot receive additional tickets.");
        }

        return board;
    }

    private static async Task<TicketBoardDefinition> GetExistingBoardAsync(
        ITicketBoardService ticketBoardService,
        int boardId)
    {
        return await ticketBoardService.GetByIdAsync(boardId)
            ?? throw new KeyNotFoundException("The current ticket board was not found.");
    }
}