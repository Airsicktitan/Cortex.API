namespace Cortex.API.DTO;

public class UpsertTicketRoutingRuleRequest
{
    public string? Department { get; set; }
    public string? TitleContains { get; set; }
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public bool IsEnabled { get; set; } = true;
}
