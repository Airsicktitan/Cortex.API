using System.Text.Json.Serialization;

namespace Cortex.API.DTO;

public class RealtimeEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string? TicketId { get; init; }
    public string? EntityId { get; init; }
    public int? ActorUserId { get; init; }
    public string? ActorDisplayName { get; init; }
    [JsonIgnore]
    public int[]? RecipientUserIds { get; init; }
    public TicketResponse? Ticket { get; init; }
    public ArchivedTicketResponse? ArchivedTicket { get; init; }
    public CommentResponse? Comment { get; init; }
    public NotificationResponse[]? Notifications { get; init; }
    public int? UnreadCount { get; init; }
    public DateTime OccurredDateUtc { get; init; } = DateTime.UtcNow;

    [JsonIgnore]
    public int[]? AudienceUserIds { get; init; }
}
