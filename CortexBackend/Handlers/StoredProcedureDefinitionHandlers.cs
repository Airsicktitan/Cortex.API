using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class StoredProcedureDefinitionHandlers
{
    public static async Task<IResult> GetStoredProcedureDefinitions(
        IStoredProcedureDefinitionService service)
    {
        var definitions = await service.GetAllAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> GetAvailableDatabaseStoredProcedures(
        IStoredProcedureDefinitionService service,
        bool includeDefinition = false)
    {
        var definitions = await service.GetAvailableStoredProceduresAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse(includeDefinition)));
    }

    public static async Task<IResult> CreateStoredProcedureDefinition(
        UpsertStoredProcedureDefinitionRequest request,
        IStoredProcedureDefinitionService service)
    {
        try
        {
            var definition = new StoredProcedureDefinition
            {
                Name = request.Name,
                ProcedureName = request.ProcedureName,
                DefinitionSql = request.DefinitionSql,
                Description = request.Description,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.CreateAsync(definition);
            return Results.Created($"/api/settings/stored-procedures/{saved.Id}", saved.ToResponse());
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> UpdateStoredProcedureDefinition(
        int id,
        UpsertStoredProcedureDefinitionRequest request,
        IStoredProcedureDefinitionService service)
    {
        try
        {
            var definition = new StoredProcedureDefinition
            {
                Name = request.Name,
                ProcedureName = request.ProcedureName,
                DefinitionSql = request.DefinitionSql,
                Description = request.Description,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.UpdateAsync(id, definition);
            return Results.Ok(saved.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> DeleteStoredProcedureDefinition(
        int id,
        IStoredProcedureDefinitionService service)
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
        catch (InvalidOperationException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }
}
