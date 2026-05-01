using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class SapReferenceEndpoints
{
    public static void MapSapReferenceEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/sap-reference")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("SAP Reference");

        g.MapGet("/sources", SapReferenceHandlers.ListSources)
            .WithName("ListSapReferenceSources")
            .Produces(StatusCodes.Status200OK);

        g.MapGet("/sources/{id:int}", SapReferenceHandlers.GetSource)
            .WithName("GetSapReferenceSource")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPost("/sources", SapReferenceHandlers.CreateSource)
            .WithName("CreateSapReferenceSource")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        g.MapPut("/sources/{id:int}", SapReferenceHandlers.UpdateSource)
            .WithName("UpdateSapReferenceSource")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPatch("/sources/{id:int}/enabled", SapReferenceHandlers.PatchSourceEnabled)
            .WithName("SetSapReferenceSourceEnabled")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/sources/{sourceId:int}", SapReferenceHandlers.DeleteSource)
            .WithName("DeleteSapReferenceSource")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/sources/{sourceId:int}/tables", SapReferenceHandlers.ListTables)
            .WithName("ListSapReferenceTables")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPost("/sources/{sourceId:int}/tables", SapReferenceHandlers.CreateTable)
            .WithName("CreateSapReferenceTable")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/tables/{tableId:int}", SapReferenceHandlers.GetTable)
            .WithName("GetSapReferenceTable")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPut("/tables/{tableId:int}", SapReferenceHandlers.UpdateTable)
            .WithName("UpdateSapReferenceTable")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/tables/{tableId:int}", SapReferenceHandlers.DeleteTable)
            .WithName("DeleteSapReferenceTable")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/tables/{tableId:int}/fields", SapReferenceHandlers.ListFields)
            .WithName("ListSapReferenceFields")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPost("/tables/{tableId:int}/fields", SapReferenceHandlers.CreateField)
            .WithName("CreateSapReferenceField")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPut("/fields/{fieldId:int}", SapReferenceHandlers.UpdateField)
            .WithName("UpdateSapReferenceField")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/fields/{fieldId:int}", SapReferenceHandlers.DeleteField)
            .WithName("DeleteSapReferenceField")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/sources/{sourceId:int}/domain-values", SapReferenceHandlers.ListDomainValues)
            .WithName("ListSapDomainValues")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPost("/sources/{sourceId:int}/domain-values", SapReferenceHandlers.CreateDomainValue)
            .WithName("CreateSapDomainValue")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapPut("/domain-values/{id:int}", SapReferenceHandlers.UpdateDomainValue)
            .WithName("UpdateSapDomainValue")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/domain-values/{id:int}", SapReferenceHandlers.DeleteDomainValue)
            .WithName("DeleteSapDomainValue")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/search", SapReferenceHandlers.Search)
            .WithName("SearchSapReferenceKnowledge")
            .Produces<SapReferenceSearchResultDto[]>()
            .Produces(StatusCodes.Status400BadRequest);
    }
}
