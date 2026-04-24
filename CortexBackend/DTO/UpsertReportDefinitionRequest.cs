namespace Cortex.API.DTO;

public class UpsertReportDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SqlQuery { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? SourceKey { get; set; }
    public string? SelectedColumns { get; set; }
}
