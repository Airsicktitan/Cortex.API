namespace Cortex.API.DTO;

public class SlaConfigurationResponse
{
    public string Priority { get; set; } = string.Empty;
    public int TargetHours { get; set; }
    public int WarningHours { get; set; }
}
