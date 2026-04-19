namespace Cortex.API.DTO;

/// <summary>Stored advisory AI triage surfaced on <see cref="TicketResponse"/> (camelCase: approvalTriagePreview).</summary>
public sealed class ApprovalTriagePreviewDto
{
    public string? Summary { get; set; }
    public string? SuggestedPriority { get; set; }
    public string? PriorityReason { get; set; }

    /// <summary>AI-suggested workflow status (controlled vocabulary from Cortex).</summary>
    public string? SuggestedStatus { get; set; }
    public List<string> MissingDetailHints { get; set; } = [];

    /// <summary>Advisory: Low, Medium, or High.</summary>
    public string? PotentialSlaRisk { get; set; }

    public string? SlaRiskReason { get; set; }
}
