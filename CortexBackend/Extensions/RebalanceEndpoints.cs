using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;

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

        rebalance.MapGet("/suggestions", RebalanceHandlers.GetSuggestions)
            .WithName("GetRebalanceSuggestions")
            .Produces<IReadOnlyList<RebalanceSuggestion>>(StatusCodes.Status200OK);

        rebalance.MapPost("/execute", RebalanceHandlers.Execute)
            .WithName("ExecuteRebalance")
            .Produces<ExecuteRebalanceResponse>(StatusCodes.Status200OK);
    }
}
