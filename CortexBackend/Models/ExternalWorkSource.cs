namespace Cortex.API.Models;

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
