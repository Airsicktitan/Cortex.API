namespace Cortex.API.DTO;

public class UpdateSessionConfigurationRequest
{
    public int InactivityTimeoutMinutes { get; set; }
    public int WarningMinutes { get; set; }
}
