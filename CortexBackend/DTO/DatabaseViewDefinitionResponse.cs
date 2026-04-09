namespace Cortex.API.DTO;

public class DatabaseViewDefinitionResponse
{
    public string ViewName { get; set; } = string.Empty;
    public string DefinitionSql { get; set; } = string.Empty;
}
