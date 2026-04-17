namespace Cortex.API.DTO;

public class UpsertTicketRoutingRuleRequest
{
    public string? BoardId { get; set; }
    public string? Priority { get; set; }
    public string? RequesterDepartment { get; set; }
    public string? RequesterRole { get; set; }
    public int RulePriority { get; set; }
    public int Weight { get; set; }
    public string? Department { get; set; }
    public string? TitleContains { get; set; }
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public bool IsEnabled { get; set; } = true;
}
