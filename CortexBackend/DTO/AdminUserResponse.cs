namespace Cortex.API.DTO;

public class AdminUserResponse
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public string? NickName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AssignmentNotificationChannel { get; set; }
    public string? SlaRiskNotificationChannel { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LastSeenDateUtc { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public string? Auth0Id { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
