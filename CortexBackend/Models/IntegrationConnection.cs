namespace Cortex.API.Models;

/// <summary>
/// A configured link to an external system (credentials, tenant, sync posture).
/// </summary>
/// <remarks>
/// Today this connection primarily drives <see cref="ExternalWorkSource"/> for work-item sync.
/// The same row type is suitable for future integrations that are not work-board sources—for example
/// SAP used only for reference data, metadata, or enrichment—by adding navigations or related entities
/// without renaming this model.
/// </remarks>
public class IntegrationConnection
{
    public int Id { get; set; }
    public IntegrationProvider Provider { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? OrganizationId { get; set; }

    /// <summary>Non-secret provider settings as JSON (validated keys only; never store secrets here).</summary>
    public string? PublicSettingsJson { get; set; }

    public IntegrationAuthMode AuthMode { get; set; } = IntegrationAuthMode.Manual;
    public IntegrationSyncMode SyncMode { get; set; } = IntegrationSyncMode.ReadOnly;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastSyncUtc { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<ExternalWorkSource> ExternalWorkSources { get; set; } = [];
}
