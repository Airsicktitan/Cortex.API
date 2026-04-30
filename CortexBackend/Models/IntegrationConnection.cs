namespace Cortex.API.Models;

public class IntegrationConnection
{
    public int Id { get; set; }
    public IntegrationProvider Provider { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? OrganizationId { get; set; }
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
