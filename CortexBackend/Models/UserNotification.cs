using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class UserNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? TicketId { get; set; }
    public bool TicketIsArchived { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadDateUtc { get; set; }
    public string? DeduplicationKey { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
