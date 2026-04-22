namespace Cortex.API.Models;

public class User
{
    public int Id { get; set; } = 0; // DB identifier
    public string? DisplayName { get; set; } // User identifier
    public string? NickName { get; set; } // Nullable
    public required string Email { get; set; } = string.Empty; // Default to empty string
    public string? PhoneNumber { get; set; } // Nullable
    public string Role { get; set; } = Auth0Roles.User;
    public string? Department { get; set; } // Nullable
    public NotificationChannelMode? AssignmentNotificationChannel { get; set; }
    public NotificationChannelMode? SlaRiskNotificationChannel { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Default to now
    public DateTime? LastLoginDate { get; set; } // Nullable
    public DateTime? LastSeenDateUtc { get; set; } // Nullable
    public DateTime? ExpiryDate { get; set; } // Nullable
    public bool IsActive { get; set; } = true; // Default to active

    /// <summary>When true, the user may be assigned as Syniti owner on tickets (directory assignment, not an Auth0 role).</summary>
    public bool IsSynitiOwnerEligible { get; set; }

    /// <summary>When true, the user may be assigned as business owner on tickets (directory assignment, not an Auth0 role).</summary>
    public bool IsBusinessOwnerEligible { get; set; }

    public string? Auth0Id { get; set; }
    public DateTime? LastModifiedDate { get; set; }

}
