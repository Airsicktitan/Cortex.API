using Microsoft.AspNetCore.Http.HttpResults;

namespace Cortex.API.DTO;

public class TicketResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set;} = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? BoardId { get; set; }
    public string BoardName { get; set; } = string.Empty;
    public int? StoryPoints { get; set; }
    public string? SynitiOwner { get; set;} = string.Empty;
    public string? BusinessOwner { get; set;} = string.Empty;
    public int CreatedBy { get; set;}
    public int LastModifiedBy { get; set;}
    public DateTime? LastModifiedDate { get; set;} = DateTime.UtcNow;

    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime SlaTargetDate { get; set; }
    public DateTime? SlaCompletedDate { get; set; }
    public string SlaStatus { get; set; } = string.Empty;
    public int SlaRemainingMinutes { get; set; }
    public bool IsSlaBreached { get; set; }
}
