using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class TicketAuditEntry
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int ChangedBy { get; set; }
    public DateTime ChangedDateUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public User? ChangedByUser { get; set; }

    public List<TicketAuditFieldChange> FieldChanges { get; set; } = [];
}
