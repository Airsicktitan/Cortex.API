namespace Cortex.API.DTO;

public class TicketAuditEntryResponse
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int ChangedBy { get; set; }
    public string ChangedByDisplayName { get; set; } = string.Empty;
    public DateTime ChangedDateUtc { get; set; }
    public List<TicketAuditFieldChangeResponse> FieldChanges { get; set; } = [];
}
