using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed record RoutingDecisionResult(
    int? MatchedRuleId,
    RoutingOutcomeType OutcomeType,
    RoutingConfidenceLevel ConfidenceLevel,
    RoutingNoMatchReason? NoMatchReason,
    string? RecommendedSynitiOwner,
    string? RecommendedBusinessOwner,
    int PrecedenceScore,
    string TieBreakKey,
    string ExplanationJson,
    string ExplanationText,
    string EngineVersion,
    int MatchedCriteriaCount);
