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

    public User? OverriddenByUser { get; set; }
}
