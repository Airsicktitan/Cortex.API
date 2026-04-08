namespace Cortex.API.DTO;

public class AdminUpdateUserRequest
{
    public string? NickName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
