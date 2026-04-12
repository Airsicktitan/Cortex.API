namespace Cortex.API.DTO;

public class UpdateNotificationChannelConfigurationRequest
{
    public string AssignmentChannel { get; set; } = "Neither";
    public string SlaRiskChannel { get; set; } = "Neither";
}
