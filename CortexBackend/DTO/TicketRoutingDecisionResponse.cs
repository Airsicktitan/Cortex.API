namespace Cortex.API.DTO;

public class TicketRoutingDecisionResponse
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int? MatchedRuleId { get; set; }
    public string OutcomeType { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string? NoMatchReason { get; set; }
    public string ChosenSynitiOwner { get; set; } = string.Empty;
    public string ChosenBusinessOwner { get; set; } = string.Empty;
    public int PrecedenceScore { get; set; }
    public string TieBreakKey { get; set; } = string.Empty;
    public string ExplanationJson { get; set; } = "{}";
    public string ExplanationText { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}

public class TicketRoutingOverrideResponse
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int OverriddenByUserId { get; set; }
    public string PreviousSynitiOwner { get; set; } = string.Empty;
    public string PreviousBusinessOwner { get; set; } = string.Empty;
    public string NewSynitiOwner { get; set; } = string.Empty;
    public string NewBusinessOwner { get; set; } = string.Empty;
    public string OverrideReasonType { get; set; } = string.Empty;
    public string OverrideReasonText { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}

public class TicketRoutingLatestResponse
{
    public TicketRoutingDecisionResponse? Decision { get; set; }
    public TicketRoutingOverrideResponse? Override { get; set; }
}
