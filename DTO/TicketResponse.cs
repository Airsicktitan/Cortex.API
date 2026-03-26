namespace Cortex.API.DTO;

public class TicketResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}