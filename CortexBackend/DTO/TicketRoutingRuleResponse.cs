namespace Cortex.API.DTO;

public class TicketRoutingRuleResponse
{
    public int Id { get; set; }
    public string Department { get; set; } = string.Empty;
    public string SynitiOwner { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
