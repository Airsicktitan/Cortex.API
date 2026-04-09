namespace Cortex.API.DTO;

public class CreateUserRequest
{
    public required string DisplayName { get; set; }
    public string? NickName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiryDate { get; set; }
}
