namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

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
    public static async Task<IResult> GetAllTickets(CortexDbContext db)
    {
        var tickets = await db.Tickets.ToListAsync();
        return Results.Ok(tickets);
    }
    
    public static async Task<IResult> GetTicketById(string id, CortexDbContext db)
    {
        var ticket = await db.Tickets.FindAsync(id);
        return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
    }

    public static async Task<IResult> GetTicketsByStatus(string status, CortexDbContext db)
    {
        var filtered = await db.Tickets
            .Where(t => t.Status == status)
            .ToListAsync();

        return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
    }
    public static async Task<IResult> GetTicketsByPriority(string priority, CortexDbContext db)
    {
        var filtered = await db.Tickets
            .Where(t => t.Priority == priority)
            .ToListAsync();

        return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
    }
    public static async Task<IResult> CreateTicket(Ticket ticket, CortexDbContext db)
    {
        // Generate next ticket number safely
            var maxNum = db.Tickets
                .Where(t => t.Id.StartsWith("TICKET-")) // filter to expected format
                .Select(t => t.Id)
                .AsEnumerable() // client-side
                .Select(id => int.Parse(id.Substring(7))) // Current bug that needs to be addressed.  This will crash if the format is unexpected. IE: "TICKET-XYZ"
                .DefaultIfEmpty(0) // handle empty case
                .Max(); // get max number


            ticket.Id = $"TICKET-{(maxNum + 1):D3}"; // format with leading zeros
            ticket.CreatedDate = DateTime.UtcNow; // set creation date
            ticket.CreatedBy = ticket.CreatedBy ?? "System"; // default creator if not provided, will change to authenticated user later. ** TODO: auth **

            db.Tickets.Add(ticket); // add to context
            await db.SaveChangesAsync(); // save to database

            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
    }

    public static async Task<IResult> UpdateTicket(string id, Ticket updatedTicket, CortexDbContext db)
    {
        var existing = await db.Tickets.FindAsync(id);
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

            await db.SaveChangesAsync(); // save changes

            return Results.Ok(existing);
    }
    public static async Task<IResult> DeleteTicket(string id, CortexDbContext db)
    {
        var existing = await db.Tickets.FindAsync(id);
            if (existing is null)
                return Results.NotFound();

            db.Tickets.Remove(existing);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Ticket deleted successfully" });
    }
}
