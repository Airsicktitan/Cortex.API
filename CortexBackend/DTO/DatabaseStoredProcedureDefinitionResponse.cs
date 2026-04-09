namespace Cortex.API.DTO;

public class DatabaseStoredProcedureDefinitionResponse
{
    public string ProcedureName { get; set; } = string.Empty;
    public string DefinitionSql { get; set; } = string.Empty;
}
