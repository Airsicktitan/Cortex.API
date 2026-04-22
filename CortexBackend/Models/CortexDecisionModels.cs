namespace Cortex.API.Models;

public sealed class WorkloadSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ActiveTicketCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int SlaRiskCount { get; set; }
    public int WorkloadScore { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CortexDecisionCandidate
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Eligible { get; set; }
    public int ActiveTicketCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int SlaRiskCount { get; set; }
    public int WorkloadScore { get; set; }
    public bool RuleMatched { get; set; }
    public bool PreferredByBoard { get; set; }
    public bool CurrentlyOverloaded { get; set; }
    public int TotalScore { get; set; }
    public List<string> Notes { get; set; } = [];
}

public sealed class CortexDecisionResult
{
    public string DecisionType { get; set; } = string.Empty;
    public string? RecommendedOwnerUserId { get; set; }
    public string? RecommendedOwnerDisplayName { get; set; }
    public string? CurrentOwnerUserId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public List<string> Reasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<CortexDecisionCandidate> Candidates { get; set; } = [];
    public Dictionary<string, string> FactorBreakdown { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? AiSummary { get; set; }
    public string? AiRiskLevel { get; set; }
    public decimal? AiConfidence { get; set; }
    public string? AiRecommendedPriority { get; set; }
    public string? AiRecommendedOwner { get; set; }
}

public sealed class RebalanceSuggestion
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string ToDisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public bool AiHighRisk { get; set; }
}

public sealed class ExecuteRebalanceResponse
{
    public int TotalEvaluated { get; set; }
    public int TotalApplied { get; set; }
    public List<AppliedRebalance> Applied { get; set; } = [];
    public List<SkippedRebalance> Skipped { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public List<string> ImpactDetails { get; set; } = [];
}

public sealed class AppliedRebalance
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class SkippedRebalance
{
    public string TicketId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
