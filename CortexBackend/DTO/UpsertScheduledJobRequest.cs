namespace Cortex.API.DTO;

public class UpsertScheduledJobRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int? StoredProcedureDefinitionId { get; set; }
}
