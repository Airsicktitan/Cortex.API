namespace Cortex.API.Extensions;

using Cortex.API.Authorization;
using Cortex.API.Handlers;
using Cortex.API.DTO;
using Cortex.API.Models;

/// <summary>
/// Ticket API: read for all authenticated roles; writes gated by capability policies.
/// </summary>
public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var tickets = app.MapGroup("/api/tickets")
            .RequireAuthorization()
            .WithTags("Tickets");

        tickets.MapGet("/archived", TicketHandlers.GetArchivedTickets)
            .WithName("GetArchivedTickets")
            .Produces<List<ArchivedTicket>>(StatusCodes.Status200OK);

        tickets.MapGet("/", TicketHandlers.GetAllTickets)
            .WithName("GetAllTickets")
            .Produces<List<Ticket>>(StatusCodes.Status200OK);

        tickets.MapGet("/{id}", TicketHandlers.GetTicketById)
            .WithName("GetTicketById")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/history", TicketHandlers.GetTicketHistory)
            .WithName("GetTicketHistory")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/status/{status}", TicketHandlers.GetTicketsByStatus)
            .WithName("GetTicketsByStatus")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/priority/{priority}", TicketHandlers.GetTicketsByPriority)
            .WithName("GetTicketsByPriority")
            .Produces<List<Ticket>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/", TicketHandlers.CreateTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("CreateTicket")
            .Produces<Ticket>(StatusCodes.Status201Created);

        tickets.MapPut("/{id}", TicketHandlers.UpdateTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("UpdateTicket")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/{id}/archive", TicketHandlers.ArchiveTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ArchiveTicket")
            .Accepts<TicketActionReasonRequest>("application/json")
            .Produces<ArchivedTicket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/archived/{id}/reactivate", TicketHandlers.ReactivateArchivedTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ReactivateArchivedTicket")
            .Produces<Ticket>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapDelete("/{id}", TicketHandlers.DeleteTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("DeleteTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
