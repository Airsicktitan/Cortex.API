namespace Cortex.API.DTO;

public class CreateTicketRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public int? BoardId { get; set; }
    public int? StoryPoints { get; set; }
    public string? Priority { get; set; } // e.g., "Low", "Medium", "High"
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public string? Status { get; set; } // e.g., "Open", "In Progress", "Closed"
    public string? ChangeReason { get; set; }

    /// <summary>Optional workflow metrics: save after Improve Request in this session.</summary>
    public IntakeAssistSaveMetrics? IntakeAssistSave { get; set; }
}
