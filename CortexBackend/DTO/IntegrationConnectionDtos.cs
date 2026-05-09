using Cortex.API.Models;

namespace Cortex.API.DTO;

public record IntegrationConnectionResponse(
    int Id,
    IntegrationProvider Provider,
    string DisplayName,
    string? TenantId,
    string? OrganizationId,
    IntegrationAuthMode AuthMode,
    IntegrationSyncMode SyncMode,
    bool IsEnabled,
    DateTime? LastSyncUtc,
    string? LastSyncStatus,
    string? LastSyncMessage,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int ExternalWorkSourceCount,
    IReadOnlyDictionary<string, string> SafeProviderSettings,
    bool CredentialConfigured,
    string? CredentialType,
    DateTime? LastValidatedAtUtc,
    string CredentialStatus,
    IReadOnlyList<string> ConfiguredCredentialFieldLabels,
    DateTime? LastCredentialUpdatedAtUtc,
    DateTime? LastCredentialRotatedAtUtc,
    IntegrationConnectionHealthDto Health);

public record CreateIntegrationConnectionRequest
{
    public IntegrationProvider Provider { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }
    public IntegrationAuthMode? AuthMode { get; init; }
    public IntegrationSyncMode? SyncMode { get; init; }
    public bool? IsEnabled { get; init; }

    /// <summary>Provider-specific non-secret fields. Secret keys are rejected by the server.</summary>
    public Dictionary<string, string?>? ProviderSettings { get; init; }
}

public record UpdateIntegrationConnectionRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }
    public IntegrationAuthMode? AuthMode { get; init; }
    public IntegrationSyncMode? SyncMode { get; init; }
    public bool? IsEnabled { get; init; }
    public DateTime? LastSyncUtc { get; init; }
    public string? LastSyncStatus { get; init; }
    public string? LastSyncMessage { get; init; }

    /// <summary>Optional partial update. Null values remove keys; omitted keys leave prior values.</summary>
    public Dictionary<string, string?>? ProviderSettings { get; init; }
}

public record SetIntegrationEnabledRequest(bool IsEnabled);
