namespace Cortex.API.DTO;

/// <summary>
/// Tier 8 autonomy evaluation result. Surfaced to the UI so operators
/// can see whether Cortex would (or did) safely act on a routing decision.
/// </summary>
public sealed class CortexAutonomyResultDto
{
    public string TicketId { get; set; } = string.Empty;
    public bool IsEligible { get; set; }
    public bool WasAutoApplied { get; set; }

    /// <summary>One of: Disabled, Shadow, AutoApplied.</summary>
    public string Mode { get; set; } = "Shadow";

    public string? RecommendedOwnerId { get; set; }
    public string? RecommendedOwnerName { get; set; }
    public string? PreviousOwnerId { get; set; }
    public double Confidence { get; set; }
    public double? LearningAdjustment { get; set; }
    public string DecisionVersion { get; set; } = "autonomy-v1";
    public List<string> PassedChecks { get; set; } = [];
    public List<string> BlockedReasons { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public DateTime? EvaluatedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
}
