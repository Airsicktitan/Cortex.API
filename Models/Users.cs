namespace Cortex.API.Models;

public class User
{
    public int Id { get; set; } = 0; // DB identifier
    public required string Username { get; set; } = string.Empty; // User identifier
    public required string Email { get; set; } = string.Empty; // Default to empty string
    public required string PasswordHash { get; set; } = string.Empty; // Hashed password
    public UserRole Role { get; set; } = UserRole.User; // Default to User role
    public string? Department { get; set; } // Nullable

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Default to now
    public DateTime? LastLoginDate { get; set; } // Nullable
    public DateTime? ExpiryDate { get; set; } // Nullable
    public bool IsActive { get; set; } = true; // Default to active
}