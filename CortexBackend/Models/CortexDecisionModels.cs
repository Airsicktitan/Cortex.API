namespace Cortex.API.Models;

public sealed class WorkloadSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ActiveTicketCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int OverdueTicketCount { get; set; }
    public int SlaRiskCount { get; set; }
    public int StaleTicketCount { get; set; }
    public decimal WorkloadScore { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CortexDecisionCandidate
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Eligible { get; set; }
    public int ActiveTicketCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int OverdueTicketCount { get; set; }
    public int SlaRiskCount { get; set; }
    public int StaleTicketCount { get; set; }
    public decimal WorkloadScore { get; set; }
    public bool RuleMatched { get; set; }
    public bool PreferredByBoard { get; set; }
    public bool CurrentlyOverloaded { get; set; }
    public decimal TotalScore { get; set; }
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

public sealed class RebalanceSuggestionAlternative
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal WorkloadScore { get; set; }
    public decimal ProjectedWorkloadScore { get; set; }
    public decimal TotalScore { get; set; }
    public string PressureLevel { get; set; } = "low";
    public int IncomingRecommendationCount { get; set; }
    public int RankBeforeDiversification { get; set; }
    public int RankAfterDiversification { get; set; }
    public string ReasonNotSelected { get; set; } = string.Empty;
}

public sealed class RebalanceSuggestion
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string TicketTitle { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string ToDisplayName { get; set; } = string.Empty;
    public string SelectedOwnerName { get; set; } = string.Empty;
    public string PreviousOwnerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public string SelectionReason { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public string RecommendationStrength { get; set; } = string.Empty;
    public List<string> Rationale { get; set; } = [];
    public List<string> ImpactPreview { get; set; } = [];
    public List<string> WhyTicketBullets { get; set; } = [];
    public List<string> WhyOwnerBullets { get; set; } = [];
    public List<string> ExpectedImpactBullets { get; set; } = [];
    public List<string> TradeoffBullets { get; set; } = [];
    public List<string> SafetyNotes { get; set; } = [];
    public List<RebalanceSuggestionAlternative> AlternativeOwners { get; set; } = [];
    public bool DiversificationApplied { get; set; }
    public string RawTopCandidateName { get; set; } = string.Empty;
    public string FinalCandidateName { get; set; } = string.Empty;
    public int CandidateRankBeforeDiversification { get; set; }
    public int CandidateRankAfterDiversification { get; set; }
    public string? AiAdvisorySummary { get; set; }
    public string? AiRiskSummary { get; set; }
    public string? AiTradeoffSummary { get; set; }
    public string? AiConfidenceWording { get; set; }
    public bool AiHighRisk { get; set; }
    public bool IsBlockedByManualOverride { get; set; }
    public string? BlockedReason { get; set; }
}

public sealed class RebalanceAiAdvisory
{
    public string TicketId { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string? RiskSummary { get; set; }
    public string? TradeoffSummary { get; set; }
    public string? ConfidenceWording { get; set; }
}

public sealed class ExecuteRebalanceRequest
{
    public List<RebalanceSuggestion> Suggestions { get; set; } = [];
    public List<string> ConfirmedManualOverrideTicketIds { get; set; } = [];
    public bool DryRun { get; set; }
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
