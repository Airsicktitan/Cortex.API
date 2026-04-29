namespace Cortex.API.DTO;

/// <summary>
/// Read-only aggregates for Tier 11 operational learning (rule health advisory only).
/// </summary>
public sealed class RoutingRuleHealthOverviewDto
{
    public List<RoutingRuleHealthRowDto> Rules { get; set; } = [];
}

/// <summary>
/// Matches ticket routing effectiveness queries in <see cref="ICortexLearningService"/>.
/// Outcome flags are scoped to tickets that ever matched this rule in <see cref="Models.TicketRoutingDecision"/>.
/// </summary>
public sealed class RoutingRuleHealthRowDto
{
    public int RuleId { get; init; }

    /// <summary>Human-readable label derived from criteria (not persisted).</summary>
    public string RuleName { get; init; } = string.Empty;

    /// <summary>Resolved board friendly name when <see cref="Models.TicketRoutingRule.BoardId"/> references a configured board.</summary>
    public string BoardName { get; init; } = string.Empty;

    public string PriorityName { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    /// <summary>
    /// Distinct tickets that have a persisted routing decision with this matched rule (same semantics as Cortex learning aggregates).
    /// </summary>
    public int MatchCount { get; init; }

    /// <summary>
    /// Terminal completions among those tickets (basis for SLA success % inside learning aggregates).
    /// </summary>
    public int SampleSize { get; init; }

    public int OverrideCount { get; init; }

    public double OverridePercent { get; init; }

    public int SlaBreachedCount { get; init; }

    /// <summary>Percent of terminal tickets that did not breach SLA (0–100).</summary>
    public double SlaSuccessPercent { get; init; }

    public int ReturnedForDetailCount { get; init; }

    public int ReassignedCount { get; init; }

    /// <summary>Utc max <see cref="Models.TicketRoutingDecision.CreatedDateUtc"/> for this rule.</summary>
    public DateTime? LastMatchedAtUtc { get; init; }

    public string HealthStatus { get; init; } = string.Empty;

    public string HealthSummary { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;
}
