namespace Cortex.API.DTO;

public record IntegrationProviderDefinitionsResponse(IReadOnlyList<IntegrationProviderDefinitionDto> Providers);

public record IntegrationProviderDefinitionDto(
    string Provider,
    string DisplayName,
    string Description,
    IReadOnlyList<string> AllowedAuthModes,
    IReadOnlyList<string> AllowedSyncModes,
    IReadOnlyList<IntegrationProviderFieldDefinitionDto> Fields,
    bool SupportsFieldDiscovery,
    bool SupportsSync,
    bool SupportsTicketCreationFromExternalItem,
    bool ReferenceMetadataOnly);

public record IntegrationProviderFieldDefinitionDto(
    string Key,
    string Label,
    string HelpText,
    string FieldType,
    bool Required,
    bool IsSecret,
    IReadOnlyList<string>? AllowedValues,
    string? Placeholder,
    string? ValidationHint);
