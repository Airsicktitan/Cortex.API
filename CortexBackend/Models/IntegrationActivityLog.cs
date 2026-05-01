namespace Cortex.API.Models;

/// <summary>
/// Audit-friendly activity trail for integration administration (discovery, sync, manual item updates).
/// </summary>
public class IntegrationActivityLog
{
    public int Id { get; set; }

    /// <summary>FK when known; optional for resilience.</summary>
    public int? IntegrationConnectionId { get; set; }

    public int ExternalWorkSourceId { get; set; }

    public IntegrationActivityType ActivityType { get; set; }

    public IntegrationActivityStatus Status { get; set; }

    public int? TriggeredByUserId { get; set; }

    public string? TriggeredByDisplayName { get; set; }

    public string? TriggeredByEmail { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public long? DurationMs { get; set; }

    public int? CreatedCount { get; set; }

    public int? UpdatedCount { get; set; }

    public int? UnchangedCount { get; set; }

    public int? SkippedCount { get; set; }

    public int? ErrorCount { get; set; }

    public int? ItemCount { get; set; }

    /// <summary>Safe user-facing summary; no secrets or raw payloads.</summary>
    public string? Message { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Optional compact summary JSON (e.g. field count only).</summary>
    public string? MetadataJson { get; set; }

    public IntegrationConnection? IntegrationConnection { get; set; }

    public ExternalWorkSource? ExternalWorkSource { get; set; }
}
