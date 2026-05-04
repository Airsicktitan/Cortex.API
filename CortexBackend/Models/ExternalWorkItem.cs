namespace Cortex.API.Models;

public class ExternalWorkItem
{
    public int Id { get; set; }
    public IntegrationProvider Provider { get; set; }
    public int ExternalWorkSourceId { get; set; }
    public string ExternalItemId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Requester { get; set; }
    public string? AssignedTo { get; set; }
    public string? Department { get; set; }
    public string? Category { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool IsDeleted { get; set; }
    public string RawJson { get; set; } = "{}";
    public string? SyncHash { get; set; }
    public string? CortexTicketId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ExternalWorkSource ExternalWorkSource { get; set; } = null!;
    public Ticket? CortexTicket { get; set; }
}
