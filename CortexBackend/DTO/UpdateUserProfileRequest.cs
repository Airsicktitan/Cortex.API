namespace Cortex.API.DTO;

public class UpdateUserProfileRequest
{
    public string? DisplayName { get; set; }
    public string? NickName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AssignmentNotificationChannel { get; set; }
    public string? SlaRiskNotificationChannel { get; set; }
}
