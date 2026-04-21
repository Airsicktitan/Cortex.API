namespace Cortex.API.DTO;

/// <summary>
/// Per-owner workload + pressure rollup surfaced by the Operational Rebalance
/// layer. Purely aggregated view over the existing OwnerWorkloadScoringService
/// output; no independent scoring lives here.
/// </summary>
public sealed class OwnerWorkloadSummaryResponse
{
    /// <summary>
    /// Raw owner key as stored on tickets (e.g. ticket.SynitiOwner). Matches
    /// the key used everywhere else in the scoring pipeline.
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Resolved human-readable owner name. Falls back to OwnerId if no
    /// matching user record is found.
    /// </summary>
    public string OwnerName { get; set; } = string.Empty;

    public int TotalOpenTickets { get; set; }

    public int HighPriorityCount { get; set; }

    public int SlaRiskCount { get; set; }

    /// <summary>
    /// Numeric workload score from OwnerWorkloadScoringService
    /// (activeCount + highPriority*2 + slaRisk*3).
    /// </summary>
    public int WorkloadScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string PressureLevel { get; set; } = "low";

    /// <summary>
    /// Count of this owner's tickets whose operational risk level is
    /// "high" or "critical" per OperationalRiskService.
    /// </summary>
    public int HighRiskTicketCount { get; set; }
}
