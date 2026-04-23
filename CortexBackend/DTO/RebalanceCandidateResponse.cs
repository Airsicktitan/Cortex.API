namespace Cortex.API.DTO;

/// <summary>
/// A single ticket surfaced as a rebalance opportunity: it belongs to an
/// overloaded owner AND is itself operationally or SLA-risky, with at least
/// one meaningfully lower-risk alternative owner available.
/// </summary>
public sealed class RebalanceCandidateResponse
{
    public string TicketId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string CurrentOwnerId { get; set; } = string.Empty;

    public string CurrentOwnerName { get; set; } = string.Empty;

    public decimal CurrentOwnerWorkloadScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string CurrentOwnerPressureLevel { get; set; } = "low";

    /// <summary>low | moderate | high | critical</summary>
    public string OperationalRiskLevel { get; set; } = "low";

    /// <summary>safe | at_risk | breached</summary>
    public string SlaRiskLevel { get; set; } = "safe";

    public int RecommendedTargetCount { get; set; }

    /// <summary>
    /// The single best suggested alternative owner, if any, so the panel
    /// can show a compact "better target available" hint without rendering
    /// the full reassignment review flow.
    /// </summary>
    public RebalanceSuggestedTargetResponse? TopSuggestedTarget { get; set; }

    /// <summary>
    /// Up to two additional ranked alternatives beyond the top target.
    /// </summary>
    public List<RebalanceSuggestedTargetResponse> AlternativeTargets { get; set; } = [];

    /// <summary>
    /// Short one-line narrative ("Better owner available", "Lower workload",
    /// etc.) used as panel microcopy — not a decision, just a hint.
    /// </summary>
    public string PotentialImpactSummary { get; set; } = string.Empty;
}

public sealed class RebalanceSuggestedTargetResponse
{
    public string OwnerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal WorkloadScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string PressureLevel { get; set; } = "low";
}
