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
        ISlaConfigurationService slaConfigurationService)
    {
        var tickets = await repo.GetAllTicketsAsync();
        var currentUser = await userContext.GetCurrentUserAsync();

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
            Status = request.Status ?? "New",
            CreatedBy = currentUser.Id,
            CreatedDate = DateTime.UtcNow
        };

        await repo.CreateTicketAsync(ticket);
        await repo.SaveChangesAsync();

        var createdTicket = await repo.GetTicketByIdAsync(ticket.Id);

        if (createdTicket is null)
            return Results.Problem("Ticket was created but could not be retrieved.");

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
        ISlaConfigurationService slaConfigurationService)
    {
        var existing = await repo.GetTicketByIdAsync(id);
        var currentUser = await userContext.GetCurrentUserAsync();

        if (existing is null)
            return Results.NotFound();

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

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        return Results.Ok(updatedTicket.ToResponse(slaConfigurations));
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
}
