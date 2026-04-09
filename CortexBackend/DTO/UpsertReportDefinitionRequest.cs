namespace Cortex.API.DTO;

public class UpsertReportDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
