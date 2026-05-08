namespace Cortex.API.Services.Integrations;

using Cortex.API.Models;

/// <summary>Declarative metadata for provider connection setup (non-secret fields may map to columns).</summary>
public sealed class IntegrationProviderFieldRule(
    string key,
    string label,
    string helpText,
    string fieldType,
    bool required,
    bool isSecret,
    string? placeholder = null,
    string? mapsToConnectionColumn = null,
    IReadOnlyList<string>? allowedValues = null,
    string? validationHint = null)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string HelpText { get; } = helpText;
    /// <summary>text, url, select, boolean, secret, textarea</summary>
    public string FieldType { get; } = fieldType;
    public bool Required { get; } = required;
    public bool IsSecret { get; } = isSecret;
    public string? Placeholder { get; } = placeholder;
    /// <summary>When set, value is read/written on <see cref="Models.IntegrationConnection"/> column instead of JSON.</summary>
    public string? MapsToConnectionColumn { get; } = mapsToConnectionColumn;
    public IReadOnlyList<string>? AllowedValues { get; } = allowedValues;
    public string? ValidationHint { get; } = validationHint;
}

public sealed class IntegrationProviderProfile(
    IntegrationProvider provider,
    string displayName,
    string description,
    IReadOnlyList<IntegrationAuthMode> allowedAuthModes,
    IReadOnlyList<IntegrationSyncMode> allowedSyncModes,
    IReadOnlyList<IntegrationProviderFieldRule> fields,
    bool supportsFieldDiscovery,
    bool supportsSync,
    bool supportsTicketCreationFromExternalItem,
    bool referenceMetadataOnly)
{
    public IntegrationProvider Provider { get; } = provider;
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
    public IReadOnlyList<IntegrationAuthMode> AllowedAuthModes { get; } = allowedAuthModes;
    public IReadOnlyList<IntegrationSyncMode> AllowedSyncModes { get; } = allowedSyncModes;
    public IReadOnlyList<IntegrationProviderFieldRule> Fields { get; } = fields;
    public bool SupportsFieldDiscovery { get; } = supportsFieldDiscovery;
    public bool SupportsSync { get; } = supportsSync;
    public bool SupportsTicketCreationFromExternalItem { get; } = supportsTicketCreationFromExternalItem;
    /// <summary>True when the provider is catalog/reference only (no live ERP/ITSM connector implied).</summary>
    public bool ReferenceMetadataOnly { get; } = referenceMetadataOnly;
}
