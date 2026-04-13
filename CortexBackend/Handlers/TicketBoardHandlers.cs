using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class TicketBoardHandlers
{
    public static async Task<IResult> GetTicketBoards(ITicketBoardService service)
    {
        var boards = await service.GetAllAsync();
        return Results.Ok(boards.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> CreateTicketBoard(
        UpsertTicketBoardDefinitionRequest request,
        ITicketBoardService service)
    {
        try
        {
            var definition = new TicketBoardDefinition
            {
                Name = request.Name ?? string.Empty,
                Description = request.Description,
                RequiresStoryPoints = request.RequiresStoryPoints,
                IsEnabled = request.IsEnabled
            };

            var savedDefinition = await service.CreateAsync(definition);
            return Results.Created(
                $"/api/boards/{savedDefinition.Id}",
                savedDefinition.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateTicketBoard(
        int id,
        UpsertTicketBoardDefinitionRequest request,
        ITicketBoardService service)
    {
        try
        {
            var definition = new TicketBoardDefinition
            {
                Name = request.Name ?? string.Empty,
                Description = request.Description,
                RequiresStoryPoints = request.RequiresStoryPoints,
                IsEnabled = request.IsEnabled
            };

            var savedDefinition = await service.UpdateAsync(id, definition);
            return Results.Ok(savedDefinition.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> DeleteTicketBoard(
        int id,
        ITicketBoardService service)
    {
        try
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
