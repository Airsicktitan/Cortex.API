using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class RebalanceEndpoints
{
    public static void MapRebalanceEndpoints(this WebApplication app)
    {
        var rebalance = app.MapGroup("/api/rebalance")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("Rebalance");

        rebalance.MapGet("/overview", RebalanceHandlers.GetOverview)
            .WithName("GetRebalanceOverview")
            .Produces<RebalanceOverviewResponse>(StatusCodes.Status200OK);
    }
}
