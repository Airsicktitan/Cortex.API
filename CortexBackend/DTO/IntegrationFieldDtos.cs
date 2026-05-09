using Cortex.API.Models;

namespace Cortex.API.DTO;

/// <summary>How a source exposes fields for mapping UX.</summary>
public enum IntegrationFieldDiscoveryMode
{
    LiveSharePointList,
    PlanningStatic,
    NotApplicable,
}

/// <summary>Advisory only: common provider fields for setup planning (no live discovery).</summary>
public sealed record PlanningFieldDefinitionDto(
    string FieldKey,
    string DisplayName,
    string DataType,
    bool IsCustom,
    bool IsRequired,
    CortexField? RecommendedCortexField,
    string? RecommendationReason,
    string ConfidenceLabel);

/// <summary>Read-only overview for the Field mapping admin experience.</summary>
public sealed record IntegrationSourceFieldsOverviewResponse(
    int SourceId,
    string SourceName,
    ExternalSourceType SourceType,
    IntegrationProvider Provider,
    string? ConnectionDisplayName,
    IntegrationFieldDiscoveryMode DiscoveryMode,
    string DiscoveryStatusMessage,
    int MappedFieldCount,
    int PlanningFieldCount,
    IReadOnlyList<ExternalFieldMappingResponse> CurrentMappings,
    IReadOnlyList<PlanningFieldDefinitionDto> PlanningFields);
