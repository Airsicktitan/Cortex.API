using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class TicketStatusEndpoints
{
    public static void MapTicketStatusEndpoints(this WebApplication app)
    {
        var ticketStatuses = app.MapGroup("/api/ticket-statuses")
            .RequireAuthorization()
            .WithTags("Ticket Statuses");

        ticketStatuses.MapGet("/", TicketStatusHandlers.GetTicketStatuses)
            .WithName("GetTicketStatuses")
            .Produces(StatusCodes.Status200OK);

        ticketStatuses.MapPost("/", TicketStatusHandlers.CreateTicketStatus)
            .RequireAuthorization("SlaManage")
            .WithName("CreateTicketStatus")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        ticketStatuses.MapPut("/{id:int}", TicketStatusHandlers.UpdateTicketStatus)
            .RequireAuthorization("SlaManage")
            .WithName("UpdateTicketStatus")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        ticketStatuses.MapDelete("/{id:int}", TicketStatusHandlers.DeleteTicketStatus)
            .RequireAuthorization("SlaManage")
            .WithName("DeleteTicketStatus")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
