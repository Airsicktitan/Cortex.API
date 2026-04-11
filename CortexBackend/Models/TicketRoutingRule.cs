namespace Cortex.API.Models;

public class TicketRoutingRule
{
    public int Id { get; set; }
    public string Department { get; set; } = string.Empty;
    public string SynitiOwner { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }
}
