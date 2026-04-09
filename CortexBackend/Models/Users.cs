namespace Cortex.API.Models;

public class User
{
    public int Id { get; set; } = 0; // DB identifier
    public string? DisplayName { get; set; } // User identifier
    public string? NickName { get; set; } // Nullable
    public required string Email { get; set; } = string.Empty; // Default to empty string
    public string? PhoneNumber { get; set; } // Nullable
    public UserRole Role { get; set; } = UserRole.User; // Default to User role
    public string? Department { get; set; } // Nullable

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Default to now
    public DateTime? LastLoginDate { get; set; } // Nullable
    public DateTime? LastSeenDateUtc { get; set; } // Nullable
    public DateTime? ExpiryDate { get; set; } // Nullable
    public bool IsActive { get; set; } = true; // Default to active

    public string? Auth0Id { get; set; }
    public DateTime? LastModifiedDate { get; set; }

}
