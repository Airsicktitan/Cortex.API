using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class TicketAuditFieldChange
{
    public int Id { get; set; }
    public int TicketAuditEntryId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    [JsonIgnore]
    public TicketAuditEntry? TicketAuditEntry { get; set; }
}
