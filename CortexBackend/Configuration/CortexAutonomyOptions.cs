namespace Cortex.API.Configuration;

/// <summary>
/// Tier 8 Safe Autonomy Layer settings. Defaults are intentionally conservative:
/// autonomy is disabled and shadow-only unless an operator explicitly opts in.
/// </summary>
public sealed class CortexAutonomyOptions
{
    public const string SectionName = "CortexAutonomy";

    /// <summary>Master switch. If false, no auto-apply ever happens regardless of <see cref="ShadowMode"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>When true, eligibility is evaluated and recorded but no ticket mutation occurs.</summary>
    public bool ShadowMode { get; set; } = true;

    /// <summary>Minimum decision confidence (0..1) for eligibility.</summary>
    public double MinConfidence { get; set; } = 0.85;

    /// <summary>Block window for recent human overrides on the same ticket.</summary>
    public int RecentOverrideWindowHours { get; set; } = 24;

    /// <summary>If true, the top recommendation must beat the next alternative by at least <see cref="MinAlternativeGap"/>.</summary>
    public bool RequireClearWinner { get; set; } = true;

    /// <summary>Required gap (0..1) between the top candidate's normalized total score and the next alternative.</summary>
    public double MinAlternativeGap { get; set; } = 0.08;

    /// <summary>True when execution (not just shadow recording) is permitted by configuration.</summary>
    public bool IsExecutionAllowed => Enabled && !ShadowMode;
}
