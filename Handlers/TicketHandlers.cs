namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;

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
        var ticket = await repo.GetTicketByIdAsync(int.Parse(id));
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
    public static async Task<IResult> CreateTicket(Ticket ticket, ITicketRepository repo)
    {
        // Generate next ticket number safely
        var tickets = await repo.GetAllTicketsAsync();

        var maxNum = tickets
            .Where(t => t.Id.StartsWith("TICKET-")) // filter to expected format
            .Select(t => t.Id)
            .Select(id => int.Parse(id.Substring(7))) // Current bug that needs to be addressed.  This will crash if the format is unexpected. IE: "TICKET-XYZ"
            .DefaultIfEmpty(0) // handle empty case
            .Max(); // get max number

        ticket.Id = $"TICKET-{(maxNum + 1):D3}"; // format with leading zeros
        ticket.CreatedDate = DateTime.UtcNow; // set creation date
        ticket.CreatedBy = ticket.CreatedBy ?? "System"; // default creator if not provided, will change to authenticated user later. ** TODO: auth **

        await repo.CreateTicketAsync(ticket);
        await repo.SaveChangesAsync();

        return Results.Created($"/api/tickets/{ticket.Id}", ticket);
    }

    public static async Task<IResult> UpdateTicket(string id, Ticket updatedTicket, ITicketRepository repo)
    {
        var existing = await repo.GetTicketByIdAsync(int.Parse(id));
            
        if (existing is null)
            return Results.NotFound();

        // Update mutable fields
        existing.Title = updatedTicket.Title;
        existing.Description = updatedTicket.Description;
        existing.Status = updatedTicket.Status;
        existing.Priority = updatedTicket.Priority;
        existing.SynitiOwner = updatedTicket.SynitiOwner;
        existing.BusinessOwner = updatedTicket.BusinessOwner;

        // Track modification
        existing.LastModifiedBy = "API User"; // TODO: auth
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
