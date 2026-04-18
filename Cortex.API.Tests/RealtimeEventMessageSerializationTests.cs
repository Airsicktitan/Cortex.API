using System.Text.Json;
using Cortex.API.DTO;

namespace Cortex.API.Tests;

public class RealtimeEventMessageSerializationTests
{
    [Fact]
    public void Serialize_DoesNotExposeRecipientUserIds()
    {
        var message = new RealtimeEventMessage
        {
            EventType = "notification.created",
            TicketId = "7001",
            EntityId = "9001",
            RecipientUserIds = [7, 9],
            AudienceUserIds = [7, 9],
            UnreadCount = 3,
        };

        var json = JsonSerializer.Serialize(
            message,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.Contains("\"eventType\":\"notification.created\"", json);
        Assert.DoesNotContain("recipientUserIds", json, StringComparison.Ordinal);
        Assert.DoesNotContain("audienceUserIds", json, StringComparison.Ordinal);
    }
}
