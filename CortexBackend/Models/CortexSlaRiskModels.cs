namespace Cortex.API.Models;

public enum CortexRiskLevel
{
    Low,
    Medium,
    High
}

public enum CortexRiskRecommendation
{
    KeepOnCurrentPath,
    RequestMoreDetail,
    Escalate,
    Reassign
}

public sealed class CortexSlaRiskAssessment
{
    public CortexRiskLevel RiskLevel { get; set; } = CortexRiskLevel.Low;
    public List<string> RiskReasons { get; set; } = [];
    public CortexRiskRecommendation Recommendation { get; set; } = CortexRiskRecommendation.KeepOnCurrentPath;
    public string RecommendationReason { get; set; } = string.Empty;

    /// <summary>0.0–1.0 deterministic confidence; reflects how many signals fired, not ML.</summary>
    public decimal Confidence { get; set; }

    /// <summary>Raw weighted score the level was derived from. Useful for diagnostics, not a UI field.</summary>
    public int Score { get; set; }

    /// <summary>SLA snapshot label at evaluation time (e.g. "On Track", "At Risk", "Breached", "Pending Approval").</summary>
    public string SlaStatus { get; set; } = string.Empty;
}
