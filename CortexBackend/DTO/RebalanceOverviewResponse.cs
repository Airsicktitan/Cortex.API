namespace Cortex.API.DTO;

/// <summary>
/// Envelope for the Operational Rebalance panel (v1):
/// - OverloadedOwners: owners currently at "high" or "critical" pressure.
/// - RebalanceCandidates: prioritized tickets under those owners that are
///   themselves operationally risky or SLA-risky, with at least one lower-risk
///   alternative owner available.
/// </summary>
public sealed class RebalanceOverviewResponse
{
    public List<OwnerWorkloadSummaryResponse> OverloadedOwners { get; set; } = [];

    public List<RebalanceCandidateResponse> RebalanceCandidates { get; set; } = [];
}
