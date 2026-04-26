namespace Cortex.API.Services;

public interface ICortexMemoryFeedbackService
{
    Task RecordAsync(
        string ticketId,
        string eventType,
        string source,
        string? relatedTicketId = null,
        int? createdByUserId = null,
        string? createdByDisplayName = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);
}
