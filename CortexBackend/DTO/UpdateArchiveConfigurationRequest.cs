namespace Cortex.API.DTO;

public class UpdateArchiveConfigurationRequest
{
    public int ArchiveAfterDays { get; set; }
    public List<string> EligibleStatuses { get; set; } = [];
}
