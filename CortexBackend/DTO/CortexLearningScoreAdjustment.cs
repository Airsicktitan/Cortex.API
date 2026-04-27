namespace Cortex.API.DTO;

/// <summary>
/// Bounded, advisory score adjustment derived from prior ticket outcomes.
/// Tier 6: surfaces in CortexDecisionResult with a visible reason. Never
/// drives auto-routing or owner assignment on its own.
/// </summary>
public sealed class CortexLearningScoreAdjustment
{
    /// <summary>One of: Owner, Rule, Decision, Risk.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// Owner display name or user id when <see cref="TargetType"/> is "Owner";
    /// rule id (string) for "Rule"; null for decision-level targets.
    /// </summary>
    public string? TargetValue { get; set; }

    /// <summary>Bounded to [-10, +10] by the producer.</summary>
    public int ScoreDelta { get; set; }

    /// <summary>One of: High, Medium, Low.</summary>
    public string Confidence { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public List<string> SupportingFacts { get; set; } = [];
}
