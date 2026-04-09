namespace Cortex.API.DTO;

public class TicketAuditFieldChangeResponse
{
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
