using Cortex.API.Models;

namespace Cortex.API.DTO;

public sealed record SharePointDiscoveredFieldResponse(
    string ExternalFieldName,
    string? ExternalFieldKey,
    string? DisplayName,
    string? Type,
    bool IsHidden,
    bool IsReadOnly,
    CortexField? SuggestedCortexField);

public sealed record ExternalSourceSyncResponse(
    int SourceId,
    string SourceName,
    IntegrationProvider Provider,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount,
    int SkippedCount,
    int ErrorCount,
    int ItemCount,
    string? Message);
