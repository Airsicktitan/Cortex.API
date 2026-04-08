namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines all ticket-related API endpoints for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.

/// - JWT authentication
/// - Role-based authorization (admin vs regular user)

/// Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// - Delete endpoint commented out pending auth implementation
/// 
/// Future Enhancements:
/// - Audit logging for all operations
/// - Input validation middleware
/// </summary>

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        // Get all tickets
        var tickets = app.MapGroup("/api/tickets")
            .RequireAuthorization()
            .WithTags("Tickets");

        tickets.MapGet("/archived", TicketHandlers.GetArchivedTickets)
            .RequireAuthorization("TicketsRead")
            .WithName("GetArchivedTickets")
            .Produces<List<ArchivedTicket>>(StatusCodes.Status200OK);

        tickets.MapGet("/", TicketHandlers.GetAllTickets)
            .RequireAuthorization("TicketsRead")
            .WithName("GetAllTickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK);

        // Get ticket by ID
        tickets.MapGet("/{id}", TicketHandlers.GetTicketById)
            .RequireAuthorization("TicketsRead")
            .WithName("GetTicketById")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by status
        tickets.MapGet("/status/{status}", TicketHandlers.GetTicketsByStatus)
            .RequireAuthorization("TicketsRead")
            .WithName("GetTicketsByStatus")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Get tickets by priority
        tickets.MapGet("/priority/{priority}", TicketHandlers.GetTicketsByPriority)
            .RequireAuthorization("TicketsRead")
            .WithName("GetTicketsByPriority")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Create new ticket
        tickets.MapPost("/", TicketHandlers.CreateTicket)
            .RequireAuthorization("TicketsCreate")
            .WithName("CreateTicket")
            .Produces<Ticket>(StatusCodes.Status201Created);

        // Update ticket
        tickets.MapPut("/{id}", TicketHandlers.UpdateTicket)
            .RequireAuthorization("TicketsUpdate")
            .WithName("UpdateTicket")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/{id}/archive", TicketHandlers.ArchiveTicket)
            .RequireAuthorization("TicketsUpdate")
            .WithName("ArchiveTicket")
            .Produces<ArchivedTicket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapDelete("/{id}", TicketHandlers.DeleteTicket)
            .RequireAuthorization("TicketsDelete")
            .WithName("DeleteTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
