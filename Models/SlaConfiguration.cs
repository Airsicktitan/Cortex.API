namespace Cortex.API.Models;

public class SlaConfiguration
{
    public string Priority { get; set; } = string.Empty;
    public int TargetHours { get; set; }
    public int WarningHours { get; set; }
}
