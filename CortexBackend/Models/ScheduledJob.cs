using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class ScheduledJob
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScheduledJobType JobType { get; set; }
    public int IntervalMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int? StoredProcedureDefinitionId { get; set; }
    public int RunAsUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }
    public DateTime? LastRunDateUtc { get; set; }
    public DateTime? NextRunDateUtc { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunMessage { get; set; }

    [JsonIgnore]
    public StoredProcedureDefinition? StoredProcedureDefinition { get; set; }

    [JsonIgnore]
    public User? RunAsUser { get; set; }
}
