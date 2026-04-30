using Cortex.API.Models;

namespace Cortex.API.DTO;

public record ExternalWorkSourceResponse(
    int Id,
    int IntegrationConnectionId,
    IntegrationProvider Provider,
    ExternalSourceType SourceType,
    string ExternalSourceId,
    string Name,
    string? ExternalUrl,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int FieldMappingCount,
    int BoardMappingCount);

public record CreateExternalWorkSourceRequest
{
    public IntegrationProvider Provider { get; init; }
    public ExternalSourceType SourceType { get; init; }
    public string ExternalSourceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ExternalUrl { get; init; }
    public bool? IsEnabled { get; init; }
}

public record UpdateExternalWorkSourceRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ExternalUrl { get; init; }
    public IntegrationProvider? Provider { get; init; }
    public ExternalSourceType? SourceType { get; init; }
    public string? ExternalSourceId { get; init; }
    public bool? IsEnabled { get; init; }
}

public record SetExternalSourceEnabledRequest(bool IsEnabled);
