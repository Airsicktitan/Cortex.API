namespace Cortex.API.Models;

public class AiSettingsAuditEntry
{
    public int Id { get; set; }
    public int? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }
    public DateTime ChangedDateUtc { get; set; }
    public string BeforeSnapshotJson { get; set; } = string.Empty;
    public string AfterSnapshotJson { get; set; } = string.Empty;
}
