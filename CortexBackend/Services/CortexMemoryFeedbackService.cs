using Cortex.API.Database;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Records Cortex Memory feedback events for future outcome analysis.
/// All writes are best-effort: errors are logged and swallowed so callers never fail.
/// </summary>
public sealed class CortexMemoryFeedbackService : ICortexMemoryFeedbackService
{
    private readonly CortexDbContext _db;
    private readonly ILogger<CortexMemoryFeedbackService> _logger;

    public CortexMemoryFeedbackService(
        CortexDbContext db,
        ILogger<CortexMemoryFeedbackService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(
        string ticketId,
        string eventType,
        string source,
        string? relatedTicketId = null,
        int? createdByUserId = null,
        string? createdByDisplayName = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketId)
            || string.IsNullOrWhiteSpace(eventType)
            || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            _db.CortexMemoryFeedbackEvents.Add(new CortexMemoryFeedbackEvent
            {
                TicketId = ticketId.Trim(),
                RelatedTicketId = string.IsNullOrWhiteSpace(relatedTicketId) ? null : relatedTicketId.Trim(),
                EventType = eventType.Trim(),
                Source = source.Trim(),
                MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
                CreatedByDisplayName = string.IsNullOrWhiteSpace(createdByDisplayName)
                    ? null
                    : createdByDisplayName.Trim(),
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cortex Memory feedback event {EventType} for ticket {TicketId} could not be recorded.",
                eventType,
                ticketId);
        }
    }
}
