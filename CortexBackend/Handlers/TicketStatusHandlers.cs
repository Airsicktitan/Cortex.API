using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class TicketStatusHandlers
{
    public static async Task<IResult> GetTicketStatuses(ITicketStatusService ticketStatusService)
    {
        var definitions = await ticketStatusService.GetAllAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> CreateTicketStatus(
        UpsertTicketStatusDefinitionRequest request,
        ITicketStatusService ticketStatusService)
    {
        try
        {
            var definition = new TicketStatusDefinition
            {
                Name = request.Name,
                Description = request.Description,
                IsEnabled = request.IsEnabled
            };

            var saved = await ticketStatusService.CreateAsync(definition);
            return Results.Created($"/api/ticket-statuses/{saved.Id}", saved.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateTicketStatus(
        int id,
        UpsertTicketStatusDefinitionRequest request,
        ITicketStatusService ticketStatusService)
    {
        try
        {
            var definition = new TicketStatusDefinition
            {
                Name = request.Name,
                Description = request.Description,
                IsEnabled = request.IsEnabled
            };

            var saved = await ticketStatusService.UpdateAsync(id, definition);
            return Results.Ok(saved.ToResponse());
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

    public static async Task<IResult> DeleteTicketStatus(
        int id,
        ITicketStatusService ticketStatusService)
    {
        try
        {
            await ticketStatusService.DeleteAsync(id);
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
