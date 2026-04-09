using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class StoredProcedureDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }
}
