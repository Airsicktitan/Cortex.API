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
/// - Delete assumes repository delete supports string ticket IDs
/// </summary>
public static class TicketHandlers
{
    public static async Task<IResult> GetAllTickets(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var tickets = await repo.GetAllTicketsAsync();
        var visibleTickets = tickets.Where(visibilityContext.CanView);
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations)));
    }

    public static async Task<IResult> GetTicketById(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService)
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

        return Results.Ok(ticket.ToResponse(slaConfigurations));
    }

    public static async Task<IResult> GetTicketHistory(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService)
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

        var history = await ticketAuditService.GetTicketHistoryAsync(id);
        return Results.Ok(history.Select(entry => entry.ToResponse()));
    }

    public static async Task<IResult> GetArchivedTickets(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var archivedTickets = await repo.GetArchivedTicketsAsync();
        var visibleTickets = archivedTickets.Where(ticket =>
            visibilityContext.CanView(ticket.CreatedBy, ticket.SynitiOwner, ticket.BusinessOwner));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse()));
    }

    public static async Task<IResult> GetTicketsByStatus(
        string status,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByStatusAsync(status);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations)));
    }

    public static async Task<IResult> GetTicketsByPriority(
        string priority,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByPriorityAsync(priority);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations)));
    }

    public static async Task<IResult> GetTicketsByUser(
        IUserContextService userContext,
        ITicketRepository repo,
        ISlaConfigurationService slaConfigurationService)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var tickets = await repo.GetTicketByUserAsync(currentUser.Id);
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(tickets.Select(ticket => ticket.ToResponse(slaConfigurations)));
    }

    public static async Task<IResult> CreateTicket(
        CreateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService)
    {
        var tickets = await repo.GetAllTicketsAsync();
        var currentUser = await userContext.GetCurrentUserAsync();
        var requestedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? await ticketStatusService.GetDefaultCreateStatusAsync()
            : request.Status.Trim();

        await ticketStatusService.EnsureSelectableStatusAsync(requestedStatus);

        var maxNum = tickets
            .Where(t => !string.IsNullOrWhiteSpace(t.Id) && t.Id.StartsWith("TICKET-"))
            .Select(t => t.Id.Substring(7))
            .Select(idPart => int.TryParse(idPart, out var num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();

        var ticket = new Ticket
        {
            Id = $"TICKET-{(maxNum + 1):D3}",
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

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Created(
            $"/api/tickets/{createdTicket.Id}",
            createdTicket.ToResponse(slaConfigurations));
    }

    public static async Task<IResult> UpdateTicket(
        string id,
        UpdateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService)
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

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(updatedTicket.ToResponse(slaConfigurations));
    }

    public static async Task<IResult> ArchiveTicket(
        string id,
        TicketActionReasonRequest? request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService)
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

        var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
        if (archivedTicket is null)
        {
            return Results.Problem("Ticket was archived but could not be retrieved.");
        }

        return Results.Ok(archivedTicket.ToResponse());
    }

    public static async Task<IResult> ReactivateArchivedTicket(
        string id,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService)
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

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(restoredTicket.ToResponse(slaConfigurations));
    }

    public static async Task<IResult> DeleteTicket(string id, ITicketRepository repo)
    {
        if (!int.TryParse(id, out var ticketId))
            return Results.BadRequest("Invalid ticket id.");

        var deleted = await repo.DeleteTicketAsync(ticketId);

        if (!deleted)
            return Results.NotFound();

        await repo.SaveChangesAsync();

        return Results.Ok(new { message = "Ticket deleted successfully" });
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
