namespace Cortex.API.DTO;

public sealed class CortexMemoryFeedbackRequest
{
    public string EventType { get; set; } = string.Empty;
    public string? RelatedTicketId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}
