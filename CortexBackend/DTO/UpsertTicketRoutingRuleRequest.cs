namespace Cortex.API.DTO;

public class UpsertTicketRoutingRuleRequest
{
    public string Department { get; set; } = string.Empty;
    public string SynitiOwner { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
