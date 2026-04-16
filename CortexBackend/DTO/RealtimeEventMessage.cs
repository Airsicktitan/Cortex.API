namespace Cortex.API.DTO;

public class RealtimeEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string? TicketId { get; init; }
    public string? EntityId { get; init; }
    public int? ActorUserId { get; init; }
    public string? ActorDisplayName { get; init; }
    public int[]? RecipientUserIds { get; init; }
    public DateTime OccurredDateUtc { get; init; } = DateTime.UtcNow;
}
