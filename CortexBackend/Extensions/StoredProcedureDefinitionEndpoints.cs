using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class StoredProcedureDefinitionEndpoints
{
    public static void MapStoredProcedureDefinitionEndpoints(this WebApplication app)
    {
        var storedProcedures = app.MapGroup("/api/settings/stored-procedures")
            .RequireAuthorization("SlaManage")
            .WithTags("Stored Procedures");

        storedProcedures.MapGet("/", StoredProcedureDefinitionHandlers.GetStoredProcedureDefinitions)
            .WithName("GetStoredProcedureDefinitions")
            .Produces(StatusCodes.Status200OK);

        storedProcedures.MapGet("/database-procedures", StoredProcedureDefinitionHandlers.GetAvailableDatabaseStoredProcedures)
            .WithName("GetAvailableDatabaseStoredProcedures")
            .Produces(StatusCodes.Status200OK);

        storedProcedures.MapPost("/", StoredProcedureDefinitionHandlers.CreateStoredProcedureDefinition)
            .WithName("CreateStoredProcedureDefinition")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        storedProcedures.MapPut("/{id:int}", StoredProcedureDefinitionHandlers.UpdateStoredProcedureDefinition)
            .WithName("UpdateStoredProcedureDefinition")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        storedProcedures.MapDelete("/{id:int}", StoredProcedureDefinitionHandlers.DeleteStoredProcedureDefinition)
            .WithName("DeleteStoredProcedureDefinition")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
