namespace Cortex.API.DTO;

public class UpsertStoredProcedureDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string DefinitionSql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}
