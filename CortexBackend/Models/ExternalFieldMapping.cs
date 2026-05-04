namespace Cortex.API.Models;

public class ExternalFieldMapping
{
    public int Id { get; set; }
    public int ExternalWorkSourceId { get; set; }
    public string ExternalFieldName { get; set; } = string.Empty;
    public string? ExternalFieldKey { get; set; }
    public CortexField CortexField { get; set; }
    public bool IsRequired { get; set; }
    public string? TransformHint { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ExternalWorkSource ExternalWorkSource { get; set; } = null!;
}
