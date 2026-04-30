using System.Text.Json;
using Cortex.API.DTO;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cortex.API.Handlers;

public static class IntegrationHandlers
{
    public static async Task<IResult> ListConnections(IExternalIntegrationService integrationService) =>
        Results.Ok(await integrationService.ListConnectionsAsync());

    public static async Task<IResult> GetConnection(int id, IExternalIntegrationService integrationService)
    {
        var connection = await integrationService.GetConnectionAsync(id);
        return connection is null ? Results.NotFound() : Results.Ok(connection);
    }

    public static async Task<IResult> CreateConnection(
        CreateIntegrationConnectionRequest request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var created = await integrationService.CreateConnectionAsync(request);
            return Results.Created($"/api/integrations/connections/{created.Id}", created);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> UpdateConnection(
        int id,
        UpdateIntegrationConnectionRequest request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var updated = await integrationService.UpdateConnectionAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> PatchConnectionEnabled(
        int id,
        SetIntegrationEnabledRequest request,
        IExternalIntegrationService integrationService)
    {
        var updated = await integrationService.SetConnectionEnabledAsync(id, request.IsEnabled);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    public static async Task<IResult> ListSources(int connectionId, IExternalIntegrationService integrationService)
    {
        var sources = await integrationService.ListSourcesAsync(connectionId);
        return sources is null ? Results.NotFound() : Results.Ok(sources);
    }

    public static async Task<IResult> CreateSource(
        int connectionId,
        CreateExternalWorkSourceRequest request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var created = await integrationService.CreateSourceAsync(connectionId, request);
            return created is null ? Results.NotFound() : Results.Created($"/api/integrations/sources/{created.Id}", created);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> UpdateSource(
        int sourceId,
        UpdateExternalWorkSourceRequest request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var updated = await integrationService.UpdateSourceAsync(sourceId, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> PatchSourceEnabled(
        int sourceId,
        SetExternalSourceEnabledRequest request,
        IExternalIntegrationService integrationService)
    {
        var updated = await integrationService.SetSourceEnabledAsync(sourceId, request.IsEnabled);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    public static async Task<IResult> GetFieldMappings(int sourceId, IExternalIntegrationService integrationService)
    {
        var mappings = await integrationService.GetFieldMappingsAsync(sourceId);
        return mappings is null ? Results.NotFound() : Results.Ok(mappings);
    }

    public static async Task<IResult> ReplaceFieldMappings(
        int sourceId,
        HttpRequest httpRequest,
        IExternalIntegrationService integrationService)
    {
        IReadOnlyList<ExternalFieldMappingItemRequest> mappings;
        try
        {
            var jsonOptions = httpRequest.HttpContext.RequestServices
                .GetRequiredService<IOptions<JsonOptions>>()
                .Value.SerializerOptions;
            mappings = await FieldMappingRequestParser.ParseRequestBodyAsync(
                httpRequest.Body,
                jsonOptions,
                httpRequest.HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return SafeErrorResponses.BadRequest();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }

        try
        {
            var result = await integrationService.ReplaceFieldMappingsAsync(sourceId, mappings);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> GetBoardMappings(int sourceId, IExternalIntegrationService integrationService)
    {
        var mappings = await integrationService.GetBoardMappingsAsync(sourceId);
        return mappings is null ? Results.NotFound() : Results.Ok(mappings);
    }

    public static async Task<IResult> ReplaceBoardMappings(
        int sourceId,
        HttpRequest httpRequest,
        IExternalIntegrationService integrationService)
    {
        IReadOnlyList<ExternalBoardMappingItemRequest> mappings;
        try
        {
            var jsonOptions = httpRequest.HttpContext.RequestServices
                .GetRequiredService<IOptions<JsonOptions>>()
                .Value.SerializerOptions;
            mappings = await BoardMappingRequestParser.ParseRequestBodyAsync(
                httpRequest.Body,
                jsonOptions,
                httpRequest.HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return SafeErrorResponses.BadRequest();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }

        try
        {
            var result = await integrationService.ReplaceBoardMappingsAsync(sourceId, mappings);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> ListWorkItems(int sourceId, IExternalIntegrationService integrationService)
    {
        var items = await integrationService.ListWorkItemsAsync(sourceId);
        return items is null ? Results.NotFound() : Results.Ok(items);
    }

    public static async Task<IResult> GetWorkItem(int itemId, IExternalIntegrationService integrationService)
    {
        var item = await integrationService.GetWorkItemAsync(itemId);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    public static async Task<IResult> ManualUpsertWorkItem(
        int sourceId,
        ManualUpsertExternalWorkItemRequest request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var item = await integrationService.ManualUpsertWorkItemAsync(sourceId, request);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> CreateTicketFromExternalWorkItem(
        int itemId,
        CreateTicketFromExternalItemRequest? request,
        IExternalIntegrationService integrationService)
    {
        try
        {
            var result = await integrationService.CreateTicketFromExternalItemAsync(
                itemId,
                request ?? new CreateTicketFromExternalItemRequest());
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ExternalWorkItemAlreadyLinkedException ex)
        {
            return Results.Json(
                new { message = ex.Message, linkedCortexTicketId = ex.LinkedCortexTicketId },
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (IntegrationApiException ex)
        {
            return SafeErrorResponses.IntegrationApi(ex);
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> DiscoverSharePointFields(int sourceId, IExternalIntegrationService integrationService)
    {
        try
        {
            var fields = await integrationService.DiscoverSharePointFieldsAsync(sourceId);
            return fields is null ? Results.NotFound() : Results.Ok(fields);
        }
        catch (IntegrationApiException ex)
        {
            return SafeErrorResponses.IntegrationApi(ex);
        }
    }

    public static async Task<IResult> SyncSharePointSource(int sourceId, IExternalIntegrationService integrationService)
    {
        try
        {
            var result = await integrationService.SyncSharePointSourceAsync(sourceId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (IntegrationApiException ex)
        {
            return SafeErrorResponses.IntegrationApi(ex);
        }
    }

    public static async Task<IResult> GetSourceReadiness(int sourceId, IExternalIntegrationService integrationService)
    {
        var readiness = await integrationService.GetSourceReadinessAsync(sourceId);
        return readiness is null ? Results.NotFound() : Results.Ok(readiness);
    }
}
