using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Operational Rebalance layer (v1): identifies overloaded owners and
/// surfaces prioritized rebalance opportunities under those owners. This is
/// a pure composition over the existing scoring / risk / recommendation
/// services — no independent scoring logic should live in implementations.
/// </summary>
public interface IRebalanceOverviewService
{
    Task<RebalanceOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default);
}
