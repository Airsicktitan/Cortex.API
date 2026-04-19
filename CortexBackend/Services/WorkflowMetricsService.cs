using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Database;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class WorkflowMetricsService(
    CortexDbContext db,
    IUserContextService userContext,
    ILogger<WorkflowMetricsService> logger)
    : IWorkflowMetricsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task TryRecordAsync(
        string eventType,
        object payload,
        string? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return;
        }

        try
        {
            int? actorId = null;
            try
            {
                var user = await userContext.GetCurrentUserAsync();
                actorId = user.Id;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Workflow metric: no actor user context.");
            }

            var json = JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions);

            db.WorkflowMetricEvents.Add(new WorkflowMetricEvent
            {
                EventType = eventType.Trim(),
                OccurredUtc = DateTime.UtcNow,
                TicketId = string.IsNullOrWhiteSpace(ticketId) ? null : ticketId.Trim(),
                ActorUserId = actorId,
                PayloadJson = json,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Workflow metric failed (non-fatal). EventType={EventType} TicketId={TicketId}",
                eventType,
                ticketId);
        }
    }
}
