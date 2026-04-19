namespace Cortex.API.Models;

/// <summary>Append-only workflow assist instrumentation (no UI).</summary>
public class WorkflowMetricEvent
{
    public long Id { get; set; }

    /// <summary>Stable event name, e.g. intake_assist_requested.</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredUtc { get; set; }

    /// <summary>Ticket id when applicable; null for pre-create-only flows.</summary>
    public string? TicketId { get; set; }

    public int? ActorUserId { get; set; }

    /// <summary>JSON payload for event-specific fields.</summary>
    public string PayloadJson { get; set; } = "{}";
}
