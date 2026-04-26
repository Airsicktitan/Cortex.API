namespace Cortex.API.Models;

/// <summary>
/// Lightweight evidence log for Cortex Memory outcome tracking.
/// Records which recommendations were shown, clicked, accepted, or overridden.
/// Never used in scoring — evidence only.
/// </summary>
public class CortexMemoryFeedbackEvent
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string? RelatedTicketId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
