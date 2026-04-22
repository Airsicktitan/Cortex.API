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

    public static async Task<IResult> GetSuggestions(
        ICortexDecisionService cortexDecisionService,
        CancellationToken cancellationToken)
    {
        var suggestions = await cortexDecisionService.GetRebalanceSuggestionsAsync(cancellationToken);
        return Results.Ok(suggestions);
    }

    public static async Task<IResult> Execute(
        ICortexDecisionService cortexDecisionService,
        CancellationToken cancellationToken)
    {
        var result = await cortexDecisionService.ExecuteRebalanceAsync(cancellationToken);
        return Results.Ok(result);
    }
}
