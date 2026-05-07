namespace Cortex.API.DTO;

public class AdminUpdateUserRequest
{
    /// <summary>When null, the caller is not updating display name.</summary>
    public string? DisplayName { get; set; }

    public string? NickName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AssignmentNotificationChannel { get; set; }
    public string? SlaRiskNotificationChannel { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsSynitiOwnerEligible { get; set; }
    public bool IsBusinessOwnerEligible { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
