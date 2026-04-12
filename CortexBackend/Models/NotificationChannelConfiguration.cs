namespace Cortex.API.Models;

public class NotificationChannelConfiguration
{
    public int Id { get; set; }
    public NotificationChannelMode AssignmentChannel { get; set; } = NotificationChannelMode.Neither;
    public NotificationChannelMode SlaRiskChannel { get; set; } = NotificationChannelMode.Neither;
}
