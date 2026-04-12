using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class TicketBoardEndpoints
{
    public static void MapTicketBoardEndpoints(this WebApplication app)
    {
        var boards = app.MapGroup("/api/ticket-boards")
            .RequireAuthorization()
            .WithTags("Ticket Boards");

        boards.MapGet("/", TicketBoardHandlers.GetTicketBoards)
            .WithName("GetTicketBoards")
            .Produces(StatusCodes.Status200OK);

        boards.MapPost("/", TicketBoardHandlers.CreateTicketBoard)
            .RequireAuthorization("SlaManage")
            .WithName("CreateTicketBoard")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        boards.MapPut("/{id:int}", TicketBoardHandlers.UpdateTicketBoard)
            .RequireAuthorization("SlaManage")
            .WithName("UpdateTicketBoard")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        boards.MapDelete("/{id:int}", TicketBoardHandlers.DeleteTicketBoard)
            .RequireAuthorization("SlaManage")
            .WithName("DeleteTicketBoard")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
