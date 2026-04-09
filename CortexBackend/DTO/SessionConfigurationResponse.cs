namespace Cortex.API.DTO;

public class SessionConfigurationResponse
{
    public int InactivityTimeoutMinutes { get; set; }
    public int WarningMinutes { get; set; }
}
