using Cortex.API.Models;

namespace Cortex.API.DTO;

/// <summary>Input for persisting one completed integration activity row.</summary>
public sealed class IntegrationActivityLogRecordRequest
{
    /// <summary>When null, <see cref="IntegrationConnectionId"/> must be set (connection-scoped activity).</summary>
    public int? ExternalWorkSourceId { get; set; }

    public int? IntegrationConnectionId { get; set; }

    public IntegrationActivityType ActivityType { get; set; }

    public IntegrationActivityStatus Status { get; set; }

    public int? TriggeredByUserId { get; set; }

    public string? TriggeredByDisplayName { get; set; }

    public string? TriggeredByEmail { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public int? CreatedCount { get; set; }

    public int? UpdatedCount { get; set; }

    public int? UnchangedCount { get; set; }

    public int? SkippedCount { get; set; }

    public int? ErrorCount { get; set; }

    public int? ItemCount { get; set; }

    public string? Message { get; set; }

    public string? ErrorMessage { get; set; }

    public string? MetadataJson { get; set; }
}

public sealed record IntegrationActivityLogResponse(
    int Id,
    int? SourceId,
    int? ConnectionId,
    IntegrationActivityType ActivityType,
    IntegrationActivityStatus Status,
    string? TriggeredByDisplayName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    long? DurationMs,
    int? CreatedCount,
    int? UpdatedCount,
    int? UnchangedCount,
    int? SkippedCount,
    int? ErrorCount,
    int? ItemCount,
    string? Message,
    string? ErrorMessage);
