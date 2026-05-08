using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this WebApplication app)
    {
        var integrations = app.MapGroup("/api/integrations")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("Integrations");

        var connections = integrations.MapGroup("/connections");

        connections.MapGet("/provider-definitions", IntegrationHandlers.ListProviderDefinitions)
            .WithName("ListIntegrationProviderDefinitions")
            .Produces<IntegrationProviderDefinitionsResponse>(StatusCodes.Status200OK);

        connections.MapGet("/", IntegrationHandlers.ListConnections)
            .WithName("ListIntegrationConnections")
            .Produces(StatusCodes.Status200OK);

        connections.MapGet("/{id:int}", IntegrationHandlers.GetConnection)
            .WithName("GetIntegrationConnection")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        connections.MapPost("/", IntegrationHandlers.CreateConnection)
            .WithName("CreateIntegrationConnection")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        connections.MapPut("/{id:int}", IntegrationHandlers.UpdateConnection)
            .WithName("UpdateIntegrationConnection")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        connections.MapPatch("/{id:int}/enabled", IntegrationHandlers.PatchConnectionEnabled)
            .WithName("SetIntegrationConnectionEnabled")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        connections.MapGet("/{connectionId:int}/credentials/status", IntegrationHandlers.GetCredentialStatus)
            .WithName("GetIntegrationConnectionCredentialStatus")
            .Produces<IntegrationCredentialStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        connections.MapPut("/{connectionId:int}/credentials", IntegrationHandlers.PutCredentials)
            .WithName("ConfigureIntegrationConnectionCredentials")
            .Produces<ConfigureIntegrationCredentialResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        connections.MapDelete("/{connectionId:int}/credentials", IntegrationHandlers.DeleteCredentials)
            .WithName("ClearIntegrationConnectionCredentials")
            .Produces<ClearIntegrationCredentialResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        connections.MapGet("/{connectionId:int}/sources", IntegrationHandlers.ListSources)
            .WithName("ListExternalWorkSources")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        connections.MapPost("/{connectionId:int}/sources", IntegrationHandlers.CreateSource)
            .WithName("CreateExternalWorkSource")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        integrations.MapPut("/sources/{sourceId:int}", IntegrationHandlers.UpdateSource)
            .WithName("UpdateExternalWorkSource")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        integrations.MapPatch("/sources/{sourceId:int}/enabled", IntegrationHandlers.PatchSourceEnabled)
            .WithName("SetExternalWorkSourceEnabled")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapGet("/sources/{sourceId:int}/field-mappings", IntegrationHandlers.GetFieldMappings)
            .WithName("GetExternalFieldMappings")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapPut("/sources/{sourceId:int}/field-mappings", IntegrationHandlers.ReplaceFieldMappings)
            .WithName("ReplaceExternalFieldMappings")
            .Accepts<ExternalFieldMappingItemRequest[]>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        integrations.MapGet("/sources/{sourceId:int}/board-mappings", IntegrationHandlers.GetBoardMappings)
            .WithName("GetExternalBoardMappings")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapPut("/sources/{sourceId:int}/board-mappings", IntegrationHandlers.ReplaceBoardMappings)
            .WithName("ReplaceExternalBoardMappings")
            .Accepts<ExternalBoardMappingItemRequest[]>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        integrations.MapGet("/sources/{sourceId:int}/readiness", IntegrationHandlers.GetSourceReadiness)
            .WithName("GetExternalSourceReadiness")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapGet("/sources/{sourceId:int}/activity", IntegrationHandlers.GetSourceActivity)
            .WithName("GetExternalSourceIntegrationActivity")
            .Produces<IntegrationActivityLogResponse[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapPost("/sources/{sourceId:int}/discover-fields", IntegrationHandlers.DiscoverSharePointFields)
            .WithName("DiscoverSharePointListFields")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status502BadGateway);

        integrations.MapPost("/sources/{sourceId:int}/sync", IntegrationHandlers.SyncSharePointSource)
            .WithName("SyncSharePointExternalSource")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

        integrations.MapGet("/sources/{sourceId:int}/items", IntegrationHandlers.ListWorkItems)
            .WithName("ListExternalWorkItems")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapGet("/items/{itemId:int}", IntegrationHandlers.GetWorkItem)
            .WithName("GetExternalWorkItem")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        integrations.MapPost("/sources/{sourceId:int}/items/manual-upsert", IntegrationHandlers.ManualUpsertWorkItem)
            .WithName("ManualUpsertExternalWorkItem")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        integrations.MapPost("/items/{itemId:int}/create-ticket", IntegrationHandlers.CreateTicketFromExternalWorkItem)
            .WithName("CreateCortexTicketFromExternalWorkItem")
            .Accepts<CreateTicketFromExternalItemRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}
