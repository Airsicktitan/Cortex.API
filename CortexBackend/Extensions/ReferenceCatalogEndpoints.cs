using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ReferenceCatalogEndpoints
{
    public static void MapReferenceCatalogEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/reference-catalogs")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("Reference catalogs");

        g.MapGet("/syniti-knowledge", ReferenceCatalogHandlers.GetSynitiKnowledgeCatalog)
            .WithName("GetSynitiKnowledgeCatalog")
            .Produces<SynitiKnowledgeCatalogListResponse>(StatusCodes.Status200OK);
    }
}
