using Cortex.API.Models;

namespace Cortex.API.DTO;

public record ExternalWorkItemResponse(
    int Id,
    IntegrationProvider Provider,
    string SourceName,
    string ExternalItemId,
    string? ExternalUrl,
    string Title,
    string? Description,
    string? Status,
    string? Priority,
    string? Requester,
    string? AssignedTo,
    string? Department,
    string? Category,
    DateTime? DueDateUtc,
    DateTime? LastModifiedUtc,
    DateTime LastSeenUtc,
    bool IsDeleted,
    string? CortexTicketId);

public record ManualUpsertExternalWorkItemRequest
{
    public string ExternalItemId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ExternalUrl { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Requester { get; init; }
    public string? AssignedTo { get; init; }
    public string? Department { get; init; }
    public string? Category { get; init; }
    public DateTime? DueDateUtc { get; init; }
    public DateTime? LastModifiedUtc { get; init; }
    public string? RawJson { get; init; }
    public string? SyncHash { get; init; }
    public string? CortexTicketId { get; init; }
}
