using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class ReportDefinitionHandlers
{
    public static async Task<IResult> GetReportDefinitions(
        IReportDefinitionService service)
    {
        var definitions = await service.GetAllAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> CreateReportDefinition(
        UpsertReportDefinitionRequest request,
        IReportDefinitionService service)
    {
        try
        {
            var definition = new ReportDefinition
            {
                Name = request.Name,
                Description = request.Description,
                SqlQuery = request.SqlQuery,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.CreateAsync(definition);
            return Results.Created($"/api/settings/reports/{saved.Id}", saved.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateReportDefinition(
        int id,
        UpsertReportDefinitionRequest request,
        IReportDefinitionService service)
    {
        try
        {
            var definition = new ReportDefinition
            {
                Name = request.Name,
                Description = request.Description,
                SqlQuery = request.SqlQuery,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.UpdateAsync(id, definition);
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

    public static async Task<IResult> DeleteReportDefinition(
        int id,
        IReportDefinitionService service)
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
    }

    public static async Task<IResult> RunReportDefinition(
        int id,
        IReportDefinitionService service)
    {
        try
        {
            var result = await service.ExecuteAsync(id);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
