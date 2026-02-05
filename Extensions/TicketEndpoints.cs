namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

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
        // Get all tickets
        var tickets = app.MapGroup("/api/tickets")
            .WithTags("Tickets");

        tickets.MapGet("/", TicketHandlers.GetAllTickets)
            .WithName("GetAllTickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK);

        // Get ticket by ID
        tickets.MapGet("/{id}", TicketHandlers.GetTicketById)
            .WithName("GetTicketById")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by status
        tickets.MapGet("/status/{status}", TicketHandlers.GetTicketsByStatus)
            .WithName("GetTicketsByStatus")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by priority
        tickets.MapGet("/priority/{priority}", TicketHandlers.GetTicketsByPriority)
            .WithName("GetTicketsByPriority")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Create new ticket
        tickets.MapPost("/", TicketHandlers.CreateTicket)
            .WithName("CreateTicket")
            .Produces<Ticket>(StatusCodes.Status201Created);

        // Update ticket
        tickets.MapPut("/{id}", TicketHandlers.UpdateTicket)
            .WithName("UpdateTicket")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapDelete("/{id}", TicketHandlers.DeleteTicket)
            .WithName("DeleteTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
