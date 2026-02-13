namespace Cortex.API.DTOs;

public class UpdateTicketRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public string? Priority { get; set; } // e.g., "Low", "Medium", "High"
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public string? Status { get; set; } // e.g., "Open", "In Progress", "Closed"
}
