using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class ReferenceCatalogHandlers
{
    public static async Task<IResult> GetSynitiKnowledgeCatalog(
        string? q,
        string? category,
        ISynitiKnowledgeCatalogReadService catalogReadService,
        CancellationToken cancellationToken)
    {
        var dto = await catalogReadService.ListAsync(q, category, cancellationToken).ConfigureAwait(false);
        return Results.Ok(dto);
    }

    public static async Task<IResult> GetSapReferenceCatalog(
        string? q,
        ISapReferenceCatalogReadService catalogReadService,
        CancellationToken cancellationToken)
    {
        var dto = await catalogReadService.ListAsync(q, cancellationToken).ConfigureAwait(false);
        return Results.Ok(dto);
    }
}
