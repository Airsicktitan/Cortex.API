namespace Cortex.API.Models;

public class TicketRoutingRule
{
    public int Id { get; set; }
    public string? Department { get; set; }
    public string? TitleContains { get; set; }
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }
}
