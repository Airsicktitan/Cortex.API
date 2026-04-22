namespace Cortex.API.DTO;

/// <summary>Phase 1 advisory triage payload. AI output is validated before persistence.</summary>
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

    /// <summary>Constrained intake category label (system vocabulary), when fusion prompts are enabled.</summary>
    public string? SuggestedCategory { get; set; }

    /// <summary>Advisory Syniti owner user id from the eligible candidate list, when fusion prompts are enabled.</summary>
    public string? SuggestedOwnerUserId { get; set; }

    /// <summary>True when the provider is not configured or generation was skipped/failed gracefully.</summary>
    public bool Unavailable { get; set; }

    public string? UnavailableReason { get; set; }
}
