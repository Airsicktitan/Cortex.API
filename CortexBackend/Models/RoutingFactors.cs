namespace Cortex.API.Models;

public sealed record RoutingFactors(
    string? BoardId,
    string? Priority,
    string? RequesterDepartment,
    string? RequesterRole,
    string? LegacyDepartment,
    string? LegacyTitle);

public enum RoutingOutcomeType
{
    RuleMatch,
    Fallback
}

public enum RoutingConfidenceLevel
{
    High,
    Medium,
    Low
}

public enum RoutingNoMatchReason
{
    NoRulesDefined,
    NoEnabledRules,
    NoCriteriaMatched,
    MissingRequiredFactors
}

public enum RoutingOverrideReasonType
{
    IncorrectRouting,
    WorkloadAdjustment,
    ManualAssignment,
    Escalation,
    Other
}
