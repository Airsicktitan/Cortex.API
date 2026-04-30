using Cortex.API.Models;

namespace Cortex.API.DTO;

public enum IntegrationReadinessCheckStatus
{
    Passed,
    Warning,
    Failed,
}

public sealed record IntegrationReadinessCheckDto(
    string Key,
    string Label,
    IntegrationReadinessCheckStatus Status,
    string Message);

public sealed record ExternalSourceReadinessResponse(
    int SourceId,
    string SourceName,
    IntegrationProvider Provider,
    ExternalSourceType SourceType,
    bool IsReady,
    bool CanDiscoverFields,
    bool CanSync,
    IReadOnlyList<IntegrationReadinessCheckDto> Checks);
