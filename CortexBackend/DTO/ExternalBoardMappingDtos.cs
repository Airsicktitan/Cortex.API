using Cortex.API.Models;

namespace Cortex.API.DTO;

public record ExternalBoardMappingResponse(
    int Id,
    int BoardId,
    string BoardName,
    ExternalBoardMappingMode MappingMode,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record ExternalBoardMappingItemRequest
{
    public int BoardId { get; init; }
    public ExternalBoardMappingMode MappingMode { get; init; } = ExternalBoardMappingMode.ReferenceOnly;
    public bool IsDefault { get; init; }
}
