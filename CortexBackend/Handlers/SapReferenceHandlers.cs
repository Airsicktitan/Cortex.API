using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class SapReferenceHandlers
{
    public static async Task<IResult> ListSources(ISapReferenceService service) =>
        Results.Ok(await service.ListSourcesAsync());

    public static async Task<IResult> GetSource(int id, ISapReferenceService service)
    {
        var s = await service.GetSourceAsync(id);
        return s is null ? Results.NotFound() : Results.Ok(s);
    }

    public static async Task<IResult> CreateSource(CreateSapReferenceSourceRequest request, ISapReferenceService service)
    {
        try
        {
            var created = await service.CreateSourceAsync(request);
            return Results.Created($"/api/sap-reference/sources/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> UpdateSource(int id, UpdateSapReferenceSourceRequest request, ISapReferenceService service)
    {
        try
        {
            var updated = await service.UpdateSourceAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> PatchSourceEnabled(int id, SetSapReferenceSourceEnabledRequest request, ISapReferenceService service)
    {
        var updated = await service.SetSourceEnabledAsync(id, request.IsEnabled);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    public static async Task<IResult> DeleteSource(int sourceId, ISapReferenceService service) =>
        await service.DeleteSourceAsync(sourceId)
            ? Results.NoContent()
            : Results.NotFound();

    public static async Task<IResult> ListTables(int sourceId, ISapReferenceService service)
    {
        var list = await service.ListTablesAsync(sourceId);
        return list.Count == 0 && await service.GetSourceAsync(sourceId) is null
            ? Results.NotFound()
            : Results.Ok(list);
    }

    public static async Task<IResult> CreateTable(int sourceId, CreateSapTableMetadataRequest request, ISapReferenceService service)
    {
        try
        {
            var created = await service.CreateTableAsync(sourceId, request);
            return created is null ? Results.NotFound() : Results.Created($"/api/sap-reference/tables/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> GetTable(int tableId, ISapReferenceService service)
    {
        var t = await service.GetTableAsync(tableId);
        return t is null ? Results.NotFound() : Results.Ok(t);
    }

    public static async Task<IResult> UpdateTable(int tableId, UpdateSapTableMetadataRequest request, ISapReferenceService service)
    {
        try
        {
            var updated = await service.UpdateTableAsync(tableId, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> DeleteTable(int tableId, ISapReferenceService service) =>
        await service.DeleteTableAsync(tableId)
            ? Results.NoContent()
            : Results.NotFound();

    public static async Task<IResult> ListFields(int tableId, ISapReferenceService service)
    {
        var list = await service.ListFieldsAsync(tableId);
        return list.Count == 0 && await service.GetTableAsync(tableId) is null
            ? Results.NotFound()
            : Results.Ok(list);
    }

    public static async Task<IResult> CreateField(int tableId, CreateSapFieldMetadataRequest request, ISapReferenceService service)
    {
        try
        {
            var created = await service.CreateFieldAsync(tableId, request);
            if (created is null)
            {
                return Results.NotFound();
            }

            return Results.Created($"/api/sap-reference/fields/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> UpdateField(int fieldId, UpdateSapFieldMetadataRequest request, ISapReferenceService service)
    {
        try
        {
            var updated = await service.UpdateFieldAsync(fieldId, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> DeleteField(int fieldId, ISapReferenceService service) =>
        await service.DeleteFieldAsync(fieldId)
            ? Results.NoContent()
            : Results.NotFound();

    public static async Task<IResult> ListDomainValues(int sourceId, ISapReferenceService service)
    {
        var list = await service.ListDomainValuesAsync(sourceId);
        return list.Count == 0 && await service.GetSourceAsync(sourceId) is null
            ? Results.NotFound()
            : Results.Ok(list);
    }

    public static async Task<IResult> CreateDomainValue(int sourceId, CreateSapDomainValueRequest request, ISapReferenceService service)
    {
        try
        {
            var created = await service.CreateDomainValueAsync(sourceId, request);
            return created is null ? Results.NotFound() : Results.Created($"/api/sap-reference/domain-values/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> UpdateDomainValue(int id, UpdateSapDomainValueRequest request, ISapReferenceService service)
    {
        try
        {
            var updated = await service.UpdateDomainValueAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    public static async Task<IResult> DeleteDomainValue(int id, ISapReferenceService service) =>
        await service.DeleteDomainValueAsync(id)
            ? Results.NoContent()
            : Results.NotFound();

    public static async Task<IResult> Search(string? query, ISapReferenceService service)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { message = "query is required." });
        }

        var results = await service.SearchAsync(query);
        return Results.Ok(results);
    }
}
