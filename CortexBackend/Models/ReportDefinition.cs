namespace Cortex.API.Models;

public class ReportDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }
}
