namespace Cortex.API.Models;

public class Ticket
{
    public string Id { get; set; } = string.Empty; // Default to empty string
    public string Title { get; set; } = string.Empty; // Default to empty string
    public string Description { get; set; } = string.Empty; // Default to empty string
    public string Status { get; set; } = "New"; // Default status
    public string Priority { get; set; } = "Medium"; // Default priority

    public string? SynitiOwner { get; set; } // Nullable
    public string? BusinessOwner { get; set; } // Nullable

    public string CreatedBy { get; set; } = string.Empty; // Default to empty string
    public DateTime CreatedDate { get; set; } = DateTime.Now; // Default to now
    public string? LastModifiedBy { get; set; } // Nullable
    public DateTime? LastModifiedDate { get; set; } // Nullable
}