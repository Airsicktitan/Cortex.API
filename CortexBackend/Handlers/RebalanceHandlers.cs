using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

/// <summary>
/// Thin HTTP surface for the Operational Rebalance layer (v1).
/// All logic lives in <see cref="IRebalanceOverviewService"/>.
/// </summary>
public static class RebalanceHandlers
{
    /// <summary>GET /api/rebalance/overview</summary>
    public static async Task<IResult> GetOverview(
        IRebalanceOverviewService rebalanceOverviewService,
        CancellationToken cancellationToken)
    {
        var overview = await rebalanceOverviewService.GetOverviewAsync(cancellationToken);
        return Results.Ok(overview);
    }
}
