namespace Cortex.API.DTO;

public class StoredProcedureDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string DefinitionSql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
