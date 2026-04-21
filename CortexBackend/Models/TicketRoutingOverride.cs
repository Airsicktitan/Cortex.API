namespace Cortex.API.Models;

public class TicketRoutingOverride
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int OverriddenByUserId { get; set; }
    public string? PreviousSynitiOwner { get; set; }
    public string? PreviousBusinessOwner { get; set; }
    public string? NewSynitiOwner { get; set; }
    public string? NewBusinessOwner { get; set; }
    public RoutingOverrideReasonType OverrideReasonType { get; set; } = RoutingOverrideReasonType.ManualAssignment;
    public string? OverrideReasonText { get; set; }
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public int? DecisionImpactPreviousOwnerId { get; set; }
    public string? DecisionImpactAssignmentField { get; set; }
    public int? DecisionImpactPreviousOwnerWorkload { get; set; }
    public string? DecisionImpactPreviousPressureLevel { get; set; }
    public string? DecisionImpactPreviousRiskLevel { get; set; }
    public string? DecisionImpactPreviousSlaStatus { get; set; }
    public DateTime? DecisionImpactAppliedAtUtc { get; set; }
    public string? DecisionImpactSource { get; set; }

    public User? OverriddenByUser { get; set; }
}
