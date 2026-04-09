namespace Cortex.API.DTO;

public class ScheduledJobResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; }
    public bool IsEnabled { get; set; }
    public int? StoredProcedureDefinitionId { get; set; }
    public string? StoredProcedureName { get; set; }
    public int RunAsUserId { get; set; }
    public string RunAsDisplayName { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
    public DateTime? LastRunDateUtc { get; set; }
    public DateTime? NextRunDateUtc { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunMessage { get; set; }
}
