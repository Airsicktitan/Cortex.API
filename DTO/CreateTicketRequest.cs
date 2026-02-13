namespace Cortex.API.DTOs;

public class CreateTicketRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public string? Priority { get; set; } // e.g., "Low", "Medium", "High"
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public string? Status { get; set; } // e.g., "Open", "In Progress", "Closed"
    public string? CreatedBy { get; set; } // will be set from authenticated user
    public DateTime CreatedDate { get; set; } // will be set in handler
    public string? Id { get; set; } // will be set in handler
}
