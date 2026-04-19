namespace Cortex.API.DTO;

/// <summary>Phase 1 advisory triage from AI (not persisted).</summary>
public class TicketTriageGenerateResponse
{
    public string? Summary { get; set; }
    public string? SuggestedPriority { get; set; }
    public string? PriorityReason { get; set; }

    /// <summary>Must match an enabled ticket status name from Cortex configuration when set.</summary>
    public string? SuggestedStatus { get; set; }
    public List<string> MissingDetails { get; set; } = [];

    /// <summary>Advisory only: Low, Medium, or High — potential delivery pressure if unclear or heavy.</summary>
    public string? PotentialSlaRisk { get; set; }

    /// <summary>One concise sentence; no breach times or invented owner workload.</summary>
    public string? SlaRiskReason { get; set; }

    /// <summary>True when the provider is not configured or generation was skipped/failed gracefully.</summary>
    public bool Unavailable { get; set; }

    public string? UnavailableReason { get; set; }
}
