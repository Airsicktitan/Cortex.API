namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using System.Security.Claims;
using Cortex.API.DTOs;
using Cortex.API.Services;

/// <summary>
/// Defines all ticket-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
/// 
/// Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// - Delete endpoint commented out pending auth implementation
/// 
/// </summary>

public static class TicketHandlers
{
    public static async Task<IResult> GetAllTickets(ITicketRepository repo)
    {
        var tickets = await repo.GetAllTicketsAsync();
        return Results.Ok(tickets);
    }
    
    public static async Task<IResult> GetTicketById(string id, ITicketRepository repo)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
    }

    public static async Task<IResult> GetTicketsByStatus(string status, ITicketRepository repo)
    {
        var filtered = await repo.GetTicketsByStatusAsync(status);

        return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
    }
    public static async Task<IResult> GetTicketsByPriority(string priority, ITicketRepository repo)
    {
        var filtered = await repo.GetTicketsByPriorityAsync(priority);

        return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
    }
    public static async Task<IResult> GetTicketsByUser(IUserContextService userContext, ITicketRepository repo, HttpContext http)
    {
        var currentUser = await userContext.GetCurrentUserAsync(http.User);
        var tickets = await repo.GetTicketByUserAsync(currentUser.Id);

        return Results.Ok(tickets);
    }
    public static async Task<IResult> CreateTicket(CreateTicketRequest request, ITicketRepository repo, IUserContextService userContext, HttpContext http)
    {
        // Generate next ticket number safely
        var tickets = await repo.GetAllTicketsAsync();
        var currentUser = await userContext.GetCurrentUserAsync(http.User);

        var maxNum = tickets
            .Where(t => t.Id.StartsWith("TICKET-")) // filter to expected format
            .Select(t => t.Id)
            .Select(id => int.Parse(id.Substring(7))) // Current bug that needs to be addressed.  This will crash if the format is unexpected. IE: "TICKET-XYZ"
            .DefaultIfEmpty(0) // handle empty case
            .Max(); // get max number

        var ticket = new Ticket
        {
            Id = $"TICKET-{(maxNum + 1):D3}", // TODO: Replace with server-side ID generation (e.g., GUID) to avoid concurrency issues
            Title = request.Title,
            Description = request.Description ?? string.Empty, // default to empty string if not provided
            Priority = request.Priority ?? "Medium", // default to "Medium" if not provided
            SynitiOwner = request.SynitiOwner,
            BusinessOwner = request.BusinessOwner,
            Status = request.Status ?? "New", // default to "New" if not provided
            CreatedBy = currentUser.Id, // track creator's name or email
            CreatedDate = DateTime.UtcNow // set creation date
        };

        await repo.CreateTicketAsync(ticket);
        await repo.SaveChangesAsync();

        var createdTicket = await repo.GetTicketByIdAsync(ticket.Id);

        return Results.Created($"/api/tickets/{ticket.Id}", createdTicket);
    }

    public static async Task<IResult> UpdateTicket(string id, UpdateTicketRequest request, ITicketRepository repo, IUserContextService userContext, HttpContext http)
    {
        var existing = await repo.GetTicketByIdAsync(id);
        var currentUser = await userContext.GetCurrentUserAsync(http.User);
            
        if (existing is null)
            return Results.NotFound();

        // Update mutable fields
        existing.Title = request.Title ?? existing.Title;
        existing.Description = request.Description ?? existing.Description;
        existing.Status = request.Status ?? existing.Status;
        existing.Priority = request.Priority ?? existing.Priority;
        existing.SynitiOwner = request.SynitiOwner ?? existing.SynitiOwner;
        existing.BusinessOwner = request.BusinessOwner ?? existing.BusinessOwner;

        // Track modification
        existing.LastModifiedBy = currentUser.Id; // authenticated user
        existing.LastModifiedDate = DateTime.UtcNow; // set modification date

        await repo.UpdateTicketAsync(existing);
        await repo.SaveChangesAsync();

        return Results.Ok(existing);
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
