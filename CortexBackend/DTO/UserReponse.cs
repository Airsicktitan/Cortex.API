using Cortex.API.Models;

namespace Cortex.API.DTO;

public class UserResponse
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
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LastSeenDateUtc { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? CreatedByDisplayName {get; set;}
}
