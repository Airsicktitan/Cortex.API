namespace Cortex.API.DTO;

public class ArchiveConfigurationResponse
{
    public int Id { get; set; }
    public int ArchiveAfterDays { get; set; }
    public List<string> EligibleStatuses { get; set; } = [];
}
