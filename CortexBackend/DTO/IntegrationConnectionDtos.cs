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
    int ExternalWorkSourceCount);

public record CreateIntegrationConnectionRequest
{
    public IntegrationProvider Provider { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }
    public IntegrationAuthMode? AuthMode { get; init; }
    public IntegrationSyncMode? SyncMode { get; init; }
    public bool? IsEnabled { get; init; }
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
}

public record SetIntegrationEnabledRequest(bool IsEnabled);
