namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

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
                .Where(t => t.Id.StartsWith("TICKET-"))
                .Select(t => t.Id)
                .AsEnumerable() // client-side
                .Select(id => int.Parse(id.Substring(7)))
                .DefaultIfEmpty(0)
                .Max();


            ticket.Id = $"TICKET-{(maxNum + 1):D3}";
            ticket.CreatedDate = DateTime.UtcNow;
            ticket.CreatedBy = ticket.CreatedBy ?? "System";

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
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
            existing.LastModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(existing);
        })
        .WithName("UpdateTicket")
        .WithTags("Tickets")
        .Produces<Ticket>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
