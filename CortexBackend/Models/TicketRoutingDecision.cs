namespace Cortex.API.Models;

public class TicketRoutingDecision
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int? MatchedRuleId { get; set; }
    public RoutingOutcomeType OutcomeType { get; set; }
    public RoutingConfidenceLevel ConfidenceLevel { get; set; }
    public RoutingNoMatchReason? NoMatchReason { get; set; }
    public string? ChosenSynitiOwner { get; set; }
    public string? ChosenBusinessOwner { get; set; }
    public int PrecedenceScore { get; set; }
    public string TieBreakKey { get; set; } = string.Empty;
    public string ExplanationJson { get; set; } = "{}";
    public string ExplanationText { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = "routing-engine-v1";
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;

    public TicketRoutingRule? MatchedRule { get; set; }
}
