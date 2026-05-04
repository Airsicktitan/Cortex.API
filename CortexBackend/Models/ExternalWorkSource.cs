namespace Cortex.API.Models;

/// <summary>
/// Configuration for ingesting external <em>work</em> items (lists, projects, ticket tables) into Cortex.
/// </summary>
/// <remarks>
/// Scoped to board-adjacent work streams (SharePoint lists, Jira, ServiceNow, etc.).
/// Do not treat this type as the only integration surface on <see cref="IntegrationConnection"/>:
/// reference/context providers (such as a future SAP metadata or lookup integration) should use
/// their own entities while still reusing <see cref="IntegrationConnection"/> for auth and identity.
/// </remarks>
public class ExternalWorkSource
{
    public int Id { get; set; }
    public int IntegrationConnectionId { get; set; }
    public IntegrationProvider Provider { get; set; }
    public ExternalSourceType SourceType { get; set; }
    public string ExternalSourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public IntegrationConnection IntegrationConnection { get; set; } = null!;
    public ICollection<ExternalBoardMapping> BoardMappings { get; set; } = [];
    public ICollection<ExternalFieldMapping> FieldMappings { get; set; } = [];
    public ICollection<ExternalWorkItem> WorkItems { get; set; } = [];
}
