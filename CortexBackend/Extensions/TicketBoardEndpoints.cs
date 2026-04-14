using Cortex.API.Authorization;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class TicketBoardEndpoints
{
    public static void MapTicketBoardEndpoints(this WebApplication app)
    {
        MapBoardGroup(app.MapGroup("/api/boards")
            .RequireAuthorization()
            .WithTags("Ticket Boards"), includeNames: true);

        MapBoardGroup(app.MapGroup("/api/ticket-boards")
            .RequireAuthorization()
            .WithTags("Ticket Boards"), includeNames: false);
    }

    private static void MapBoardGroup(RouteGroupBuilder boards, bool includeNames)
    {
        var getBoards = boards.MapGet("/", TicketBoardHandlers.GetTicketBoards)
            .Produces(StatusCodes.Status200OK);
        if (includeNames)
        {
            getBoards.WithName("GetTicketBoards");
        }

        var createBoard = boards.MapPost("/", TicketBoardHandlers.CreateTicketBoard)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
        if (includeNames)
        {
            createBoard.WithName("CreateTicketBoard");
        }

        var updateBoard = boards.MapPut("/{id:int}", TicketBoardHandlers.UpdateTicketBoard)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
        if (includeNames)
        {
            updateBoard.WithName("UpdateTicketBoard");
        }

        var deleteBoard = boards.MapDelete("/{id:int}", TicketBoardHandlers.DeleteTicketBoard)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
        if (includeNames)
        {
            deleteBoard.WithName("DeleteTicketBoard");
        }
    }
}
