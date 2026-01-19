namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Data;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var tickets = SampleData.GetSampleTickets();

        // Root endpoint
        app.MapGet("/", () => "🧠 CORTEX Online - Central Operations & Routing Technology EXpert")
            .WithName("Root")
            .WithTags("Health");

        // Get all tickets
        app.MapGet("/api/tickets", () => tickets)
            .WithName("GetAllTickets")
            .WithTags("Tickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK);

        // Get ticket by ID
        app.MapGet("/api/tickets/{id}", (string id) =>
        {
            var ticket = tickets.FirstOrDefault(t => t.Id == id);
            return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
        })
            .WithName("GetTicketById")
            .WithTags("Tickets")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by status
        app.MapGet("/api/tickets/status/{status}", (string status) =>
        {
            var filtered = tickets.Where(t => t.Status == status).ToList();
            return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
        })
            .WithName("GetTicketsByStatus")
            .WithTags("Tickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by priority
        app.MapGet("/api/tickets/priority/{priority}", (string priority) =>
        {
            var filtered = tickets.Where(t => t.Priority == priority).ToList();
            return filtered.Any() ? Results.Ok(filtered) : Results.NotFound();
        })
            .WithName("GetTicketsByPriority")
            .WithTags("Tickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Create new ticket
        app.MapPost("/api/tickets", (Ticket ticket) =>
        {
            var maxNum = tickets
                .Select(t => int.Parse(t.Id.Replace("TICKET-", "")))
                .DefaultIfEmpty(0)
                .Max();

            ticket.Id = $"TICKET-{(maxNum + 1):D3}";
            ticket.CreatedDate = DateTime.UtcNow;
            ticket.CreatedBy = ticket.CreatedBy ?? "System";
            tickets.Add(ticket);

            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
        })
            .WithName("CreateTicket")
            .WithTags("Tickets")
            .Produces<Ticket>(StatusCodes.Status201Created);

        // Update ticket
        app.MapPut("/api/tickets/{id}", (string id, Ticket updatedTicket) =>
        {
            var oldTicket = tickets.FirstOrDefault(t => t.Id == id);
            if (oldTicket is null)
                return Results.NotFound();

            var index = tickets.IndexOf(oldTicket);

            // Preserve truly immutable fields only
            updatedTicket.Id = id;
            updatedTicket.CreatedBy = oldTicket.CreatedBy;
            updatedTicket.CreatedDate = oldTicket.CreatedDate;

            // Track modification
            updatedTicket.LastModifiedBy = "API User"; // TODO: Get from authentication
            updatedTicket.LastModifiedDate = DateTime.UtcNow;

            tickets[index] = updatedTicket;

            return Results.Ok(updatedTicket);
        })
            .WithName("UpdateTicket")
            .WithTags("Tickets")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}