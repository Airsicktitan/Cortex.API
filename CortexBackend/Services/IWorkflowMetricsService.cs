namespace Cortex.API.Services;

/// <summary>Best-effort append-only workflow metrics. Never throws to callers.</summary>
public interface IWorkflowMetricsService
{
    /// <summary>Records one event. Failures are logged only.</summary>
    Task TryRecordAsync(
        string eventType,
        object payload,
        string? ticketId = null,
        CancellationToken cancellationToken = default);
}
