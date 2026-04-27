namespace Cortex.API.Models;

/// <summary>
/// Audit row for a Tier 8 autonomy evaluation. Recorded for every evaluation,
/// regardless of whether the recommendation was eligible or auto-applied.
/// </summary>
public class CortexAutonomyDecision
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string? RecommendedOwnerId { get; set; }
    public string? RecommendedOwnerName { get; set; }
    public string? PreviousOwnerId { get; set; }
    public decimal Confidence { get; set; }
    public decimal? LearningAdjustment { get; set; }
    public bool IsEligible { get; set; }
    public bool WasAutoApplied { get; set; }

    /// <summary>One of: Disabled, Shadow, AutoApplied.</summary>
    public string Mode { get; set; } = "Shadow";

    public string PassedChecksJson { get; set; } = "[]";
    public string BlockedReasonsJson { get; set; } = "[]";
    public string Summary { get; set; } = string.Empty;
    public string DecisionVersion { get; set; } = "autonomy-v1";
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedDateUtc { get; set; }
}
