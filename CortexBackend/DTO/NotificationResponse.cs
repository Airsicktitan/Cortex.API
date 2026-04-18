namespace Cortex.API.DTO;

public class NotificationResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = "system";
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TicketId { get; set; }
    public bool TicketIsArchived { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ReadDateUtc { get; set; }
}

public class NotificationFeedResponse
{
    public int UnreadCount { get; set; }
    public List<NotificationResponse> Items { get; set; } = [];
}
