using Cortex.API.Models;

namespace Cortex.API.DTO;

public record ExternalFieldMappingResponse(
    int Id,
    string ExternalFieldName,
    string? ExternalFieldKey,
    CortexField CortexField,
    bool IsRequired,
    string? TransformHint,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record ExternalFieldMappingItemRequest
{
    public string ExternalFieldName { get; init; } = string.Empty;
    public string? ExternalFieldKey { get; init; }
    public CortexField CortexField { get; init; }
    public bool IsRequired { get; init; }
    public string? TransformHint { get; init; }
}
