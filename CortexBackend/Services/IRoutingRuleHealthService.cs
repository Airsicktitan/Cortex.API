using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Read-only routing rule aggregates for Tier 11 admin visibility — never mutates rules or routing.
/// </summary>
public interface IRoutingRuleHealthService
{
    Task<RoutingRuleHealthOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
