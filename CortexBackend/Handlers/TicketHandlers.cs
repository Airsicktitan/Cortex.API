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
            visibleTickets.Select(ticket => ticket.CreatedBy));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
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
        var mappingContext = await mappingContextFactory.CreateAsync([ticket.CreatedBy]);

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
            visibleTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }));

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
            visibleTickets.Select(ticket => ticket.CreatedBy));

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
            visibleTickets.Select(ticket => ticket.CreatedBy));

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
            tickets.Select(ticket => ticket.CreatedBy));

        return Results.Ok(tickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> CreateTicket(
        CreateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var nextTicketId = await repo.GetNextTicketIdAsync();
        var currentUser = await userContext.GetCurrentUserAsync();
        var requestedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? await ticketStatusService.GetDefaultCreateStatusAsync()
            : request.Status.Trim();

        await ticketStatusService.EnsureSelectableStatusAsync(requestedStatus);

        var ticket = new Ticket
        {
            Id = nextTicketId,
            Title = request.Title,
            Description = request.Description ?? string.Empty,
            Priority = request.Priority ?? "Medium",
            SynitiOwner = request.SynitiOwner,
            BusinessOwner = request.BusinessOwner,
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
        var mappingContext = await mappingContextFactory.CreateAsync([createdTicket.CreatedBy]);

        return Results.Created(
            $"/api/tickets/{createdTicket.Id}",
            createdTicket.ToResponse(slaConfigurations, mappingContext));
    }

    public static async Task<IResult> UpdateTicket(
        string id,
        UpdateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
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

        existing.Title = request.Title ?? existing.Title;
        existing.Description = request.Description ?? existing.Description;
        existing.Status = request.Status ?? existing.Status;
        existing.Priority = request.Priority ?? existing.Priority;
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
        var mappingContext = await mappingContextFactory.CreateAsync([updatedTicket.CreatedBy]);

        return Results.Ok(updatedTicket.ToResponse(slaConfigurations, mappingContext));
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
            [archivedTicket.CreatedBy, archivedTicket.ArchivedBy]);

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
        var mappingContext = await mappingContextFactory.CreateAsync([restoredTicket.CreatedBy]);

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
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate
        };
    }
}
