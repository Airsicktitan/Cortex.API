namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines all ticket-related API endpoints for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
/// 
/// Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// - Delete endpoint commented out pending auth implementation
/// 
/// Future Enhancements:
/// - JWT authentication
/// - Role-based authorization (admin vs regular user)
/// - Audit logging for all operations
/// - Input validation middleware
/// </summary>

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        // Root endpoint
        app.MapGet("/", () => "🧠 CORTEX Online - Central Operations & Routing Technology EXpert")
            .WithName("Root")
            .WithTags("Health");

        // Get all tickets
        app.MapGet("/api/tickets", async (CortexDbContext db) =>
        {
            return await db.Tickets.ToListAsync();
        })
        .WithName("GetAllTickets")
        .WithTags("Tickets")
        .Produces<List<Ticket>>(StatusCodes.Status200OK);

        // Get ticket by ID
        app.MapGet("/api/tickets/{id}", async (string id, CortexDbContext db) =>
        {
            var ticket = await db.Tickets.FindAsync(id);
            return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
        })
        .WithName("GetTicketById")
        .WithTags("Tickets")
        .Produces<Ticket>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Get tickets by status
        app.MapGet("/api/tickets/status/{status}", async (string status, CortexDbContext db) =>
        {
            var filtered = await db.Tickets
                .Where(t => t.Status == status)
                .ToListAsync();

            return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
        })
        .WithName("GetTicketsByStatus")
        .WithTags("Tickets")
        .Produces<List<Ticket>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Get tickets by priority
        app.MapGet("/api/tickets/priority/{priority}", async (string priority, CortexDbContext db) =>
        {
            var filtered = await db.Tickets
                .Where(t => t.Priority == priority)
                .ToListAsync();

            return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
        })
        .WithName("GetTicketsByPriority")
        .WithTags("Tickets")
        .Produces<List<Ticket>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Create new ticket
        app.MapPost("/api/tickets", async (Ticket ticket, CortexDbContext db) =>
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

            return Results.Created($"/api/tickets/{ticket.Id}", ticket); // return created response
        })
        .WithName("CreateTicket")
        .WithTags("Tickets")
        .Produces<Ticket>(StatusCodes.Status201Created);

        // Update ticket
        app.MapPut("/api/tickets/{id}", async (string id, Ticket updatedTicket, CortexDbContext db) =>
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
        })
        .WithName("UpdateTicket")
        .WithTags("Tickets")
        .Produces<Ticket>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Delete ticket ** TODO: add auth/roles later and add delete API call to db **
        // app.MapDelete("/api/tickets/{id}", async (string id, CortexDbContext db) => {}
    }
}
