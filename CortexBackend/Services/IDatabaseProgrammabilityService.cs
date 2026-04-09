namespace Cortex.API.Services;

public record DatabaseViewDefinition(string ViewName, string DefinitionSql);
public record DatabaseStoredProcedureDefinition(string ProcedureName, string DefinitionSql);

public interface IDatabaseProgrammabilityService
{
    Task<IReadOnlyList<DatabaseViewDefinition>> GetUserViewsAsync();
    Task<IReadOnlyList<DatabaseStoredProcedureDefinition>> GetUserStoredProceduresAsync();
    Task CreateOrAlterViewAsync(string viewName, string definitionSql);
    Task DropViewAsync(string viewName);
    Task CreateOrAlterStoredProcedureAsync(string procedureName, string definitionSql);
    Task DropStoredProcedureAsync(string procedureName);
}
