namespace Cortex.API.Models;

public class SessionConfiguration
{
    public int Id { get; set; }
    public int InactivityTimeoutMinutes { get; set; }
    public int WarningMinutes { get; set; }
}
